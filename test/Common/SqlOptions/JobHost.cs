// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    /// <summary>
    /// A <see cref="JobHost"/> is the execution container for jobs. Once started, the
    /// <see cref="JobHost"/> will manage and run job functions when they are triggered.
    /// </summary>
    public class JobHost : IJobHost, IDisposable
    {
        private const int StateNotStarted = 0;
        private const int StateStarting = 1;
        private const int StateStarted = 2;
        private const int StateStoppingOrStopped = 3;

        private readonly IJobHostContextFactory _jobHostContextFactory;
        private readonly CancellationTokenSource _shutdownTokenSource;
        private readonly CancellationTokenSource _stoppingTokenSource;

        private JobHostContext _context;

        // Null if we haven't yet started runtime initialization.
        // Points to an incomplete task during initialization.
        // Points to a completed task after initialization.
        private Task _hostInitializationTask = null;

        private bool HasInitialized => this._hostInitializationTask?.IsCompleted ?? false;

        private int _state;
        private Task _stopTask;
        private bool _disposed;
        private readonly WebJobs.JobHost _host;
        // Common lock to protect fields.
        private readonly object _lock = new();

        private ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="JobHost"/> class using the configuration provided.
        /// </summary>
        public JobHost(IOptions<JobHostOptions> options, IJobHostContextFactory jobHostContextFactory)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this._jobHostContextFactory = jobHostContextFactory;
            this._shutdownTokenSource = new CancellationTokenSource();
            this._stoppingTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this._shutdownTokenSource.Token);
            this._host = new(options, jobHostContextFactory);
        }

        public JobHost()
        {
        }

        // Test hook only.
        internal IListener Listener { get; set; }

        /// <summary>Starts the host.</summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> that will start the host.</returns>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();

            if (Interlocked.CompareExchange(ref this._state, StateStarting, StateNotStarted) != StateNotStarted)
            {
                throw new InvalidOperationException("Start has already been called.");
            }

            return this.StartAsyncCore(cancellationToken);
        }

        protected virtual async Task StartAsyncCore(CancellationToken cancellationToken)
        {
            await this.EnsureHostInitializedAsync(cancellationToken);

            await this.Listener.StartAsync(cancellationToken);

            this.OnHostStarted();

            string msg = "Job host started";
            this._logger?.LogInformation(msg);

            this._state = StateStarted;
        }

        /// <summary>Stops the host.</summary>
        /// <returns>A <see cref="Task"/> that will stop the host.</returns>
        public Task StopAsync()
        {
            this.ThrowIfDisposed();

            Interlocked.CompareExchange(ref this._state, StateStoppingOrStopped, StateStarted);

            if (this._state != StateStoppingOrStopped)
            {
                throw new InvalidOperationException("The host has not yet started.");
            }

            // Multiple threads may call StopAsync concurrently. Both need to return the same task instance.
            lock (this._lock)
            {
                if (this._stopTask == null)
                {
                    this._stoppingTokenSource.Cancel();
                    this._stopTask = this.StopAsyncCore(CancellationToken.None);
                }
            }

            return this._stopTask;
        }

        protected virtual async Task StopAsyncCore(CancellationToken cancellationToken)
        {
            await this.Listener.StopAsync(cancellationToken);

            // Flush remaining logs
            await this._context.EventCollector.FlushAsync(cancellationToken);

            string msg = "Job host stopped";
            this._logger?.LogInformation(msg);
        }

        /// <summary>Calls a job method.</summary>
        /// <param name="method">The job method to call.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> that will call the job method.</returns>
        public Task CallAsync(MethodInfo method, CancellationToken cancellationToken = default)
        {
            IDictionary<string, object> argumentsDictionary = null;
            return this.CallAsync(method, argumentsDictionary, cancellationToken);
        }

        /// <summary>Calls a job method.</summary>
        /// <param name="method">The job method to call.</param>
        /// <param name="arguments">
        /// An object with public properties representing argument names and values to bind to parameters in the job
        /// method. In addition to parameter values, these may also include binding data values. 
        /// </param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> that will call the job method.</returns>
        public Task CallAsync(MethodInfo method, object arguments, CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();

            IDictionary<string, object> argumentsDictionary = ObjectDictionaryConverter.AsDictionary(arguments);
            return this.CallAsync(method, argumentsDictionary, cancellationToken);
        }

        /// <summary>Calls a job method.</summary>
        /// <param name="method">The job method to call.</param>
        /// <param name="arguments">
        /// An object with public properties representing argument names and values to bind to parameters in the job
        /// method. In addition to parameter values, these may also include binding data values. 
        /// </param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> that will call the job method.</returns>
        public Task CallAsync(string method, object arguments, CancellationToken cancellationToken = default)
        {
            this.ThrowIfDisposed();

            IDictionary<string, object> argumentsDictionary = ObjectDictionaryConverter.AsDictionary(arguments);
            return this.CallAsync(method, argumentsDictionary, cancellationToken);
        }

        /// <summary>Calls a job method.</summary>
        /// <param name="method">The job method to call.</param>
        /// <param name="arguments">The argument names and values to bind to parameters in the job method. In addition to parameter values, these may also include binding data values. </param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> that will call the job method.</returns>
        public Task CallAsync(MethodInfo method, IDictionary<string, object> arguments, CancellationToken cancellationToken = default)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            this.ThrowIfDisposed();

            async Task CallAsyncAwaited(MethodInfo method, IDictionary<string, object> arguments, CancellationToken cancellationToken)
            {
                await this.EnsureHostInitializedAsync(cancellationToken);
                IFunctionDefinition function = this._context.FunctionLookup.Lookup(method);
                await this.CallAsyncCore(function, method, arguments, cancellationToken);
            }

            // Skip async state machine if we're initialized
            if (!this.HasInitialized)
            {
                return CallAsyncAwaited(method, arguments, cancellationToken);
            }

            IFunctionDefinition function = this._context.FunctionLookup.Lookup(method);
            return this.CallAsyncCore(function, method, arguments, cancellationToken);
        }

        /// <summary>Calls a job method.</summary>
        /// <param name="name">The name of the function to call.</param>
        /// <param name="arguments">The argument names and values to bind to parameters in the job method. In addition to parameter values, these may also include binding data values. </param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> that will call the job method.</returns>
        public Task CallAsync(string name, IDictionary<string, object> arguments = null, CancellationToken cancellationToken = default)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            this.ThrowIfDisposed();

            async Task CallAsyncAwaited(IDictionary<string, object> arguments, CancellationToken cancellationToken)
            {
                await this.EnsureHostInitializedAsync(cancellationToken);
                IFunctionDefinition function = this._context.FunctionLookup.LookupByName(name);
                await this.CallAsyncCore(function, name, arguments, cancellationToken);
            }

            // Skip async state machine if we're initialized
            if (!this.HasInitialized)
            {
                return CallAsyncAwaited(arguments, cancellationToken);
            }

            IFunctionDefinition function = this._context.FunctionLookup.LookupByName(name);

            return this.CallAsyncCore(function, name, arguments, cancellationToken);
        }


        private async Task CallAsyncCore(IFunctionDefinition function, object functionKey, IDictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (function == null)
            {
                throw new InvalidOperationException($"'{functionKey}' can't be invoked from Azure WebJobs SDK. Is it missing Azure WebJobs SDK attributes?");
            }

            IFunctionInstance instanceFactory = CreateFunctionInstance(function, arguments);
            IDelayedException exception = await this._context.Executor.TryExecuteAsync(instanceFactory, cancellationToken);

            exception?.Throw();
        }

        /// <summary>
        /// Dispose the instance
        /// </summary>
        /// <param name="disposing">True if currently disposing.</param>
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_shutdownTokenSource")]
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !this._disposed)
            {
                // Running callers might still be using this cancellation token.
                // Mark it canceled but don't dispose of the source while the callers are running.
                // Otherwise, callers would receive ObjectDisposedException when calling token.Register.
                // For now, rely on finalization to clean up _shutdownTokenSource's wait handle (if allocated).
                this._shutdownTokenSource.Cancel();

                this._stoppingTokenSource.Dispose();

                this._context?.Dispose();

                this._disposed = true;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private static IFunctionInstance CreateFunctionInstance(IFunctionDefinition func, IDictionary<string, object> parameters)
        {
            var context = new FunctionInstanceFactoryContext
            {
                Id = Guid.NewGuid(),
                ParentId = null,
                ExecutionReason = ExecutionReason.HostCall,
                Parameters = parameters
            };
            return func.InstanceFactory.Create(context);
        }

        private void ThrowIfDisposed()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }

        /// <summary>
        /// Ensure all required host services are initialized and the host is ready to start
        /// processing function invocations. This function does not start the listeners.
        /// If multiple threads call this, only one should do the initialization. The rest should wait.
        /// When this task is signalled, _context is initialized.
        /// </summary>
        private Task EnsureHostInitializedAsync(CancellationToken cancellationToken)
        {
            if (this._context != null)
            {
                return Task.CompletedTask;
            }

            TaskCompletionSource<bool> tcs = null;

            if (this._hostInitializationTask == null)
            {
                lock (this._lock)
                {
                    if (this._hostInitializationTask == null)
                    {
                        // This thread wins the race and owns initialing. 
                        tcs = new TaskCompletionSource<bool>();
                        this._hostInitializationTask = tcs.Task;
                    }
                }
            }

            if (tcs != null)
            {
                // Ignore the return value and use tcs so that all threads are awaiting the same thing. 
                _ = this.InitializeHostAsync(tcs, cancellationToken);
            }

            return this._hostInitializationTask;
        }

        // Caller gaurantees this is single-threaded. 
        // Set initializationTask when complete, many threads can wait on that. 
        // When complete, the fields should be initialized to allow runtime usage. 
        private async Task InitializeHostAsync(TaskCompletionSource<bool> initializationTask, CancellationToken cancellationToken)
        {
            try
            {
                JobHostContext context = await this._jobHostContextFactory.Create(this._host, this._shutdownTokenSource.Token, cancellationToken);

                this._context = context;
                this.Listener = context.Listener;
                this._logger = this._context.LoggerFactory?.CreateLogger(LogCategories.Startup);

                initializationTask.SetResult(true);
            }
            catch (Exception e)
            {
                initializationTask.SetException(e);
            }
        }

        /// <summary>
        /// Called when host initialization has been completed, but before listeners
        /// are started.
        /// </summary>
        protected internal virtual void OnHostInitialized()
        {
        }

        /// <summary>
        /// Called when all listeners have started and the host is running.
        /// </summary>
        protected virtual void OnHostStarted()
        {
        }
    }

    internal static class ObjectDictionaryConverter
    {
        public static IDictionary<string, object> AsDictionary(object values)
        {
            if (values == null)
            {
                return null;
            }
#pragma warning disable IDE0019
            var valuesAsDictionary = values as IDictionary<string, object>;

            if (valuesAsDictionary != null)
            {
                return valuesAsDictionary;
            }

            var valuesAsNonGenericDictionary = values as System.Collections.IDictionary;

            if (valuesAsNonGenericDictionary != null)
            {
                throw new InvalidOperationException("Argument dictionaries must implement IDictionary<string, object>.");
            }

            IDictionary<string, object> dictionary = new Dictionary<string, object>();

            foreach (PropertyHelper property in PropertyHelper.GetProperties(values))
            {
                // Extract the property values from the property helper
                // The advantage here is that the property helper caches fast accessors.
                dictionary.Add(property.Name, property.GetValue(values));
            }

            return dictionary;
        }
    }

    // Original code here: https://github.com/aspnet/Common/blob/dev/src/Microsoft.Extensions.PropertyHelper.Sources/PropertyHelper.cs
    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "This class is designed for extensibility and has virtual members.")]
    internal class PropertyHelper
    {
        private static readonly MethodInfo CallPropertyGetterOpenGenericMethod =
            typeof(PropertyHelper).GetTypeInfo().GetDeclaredMethod(nameof(CallPropertyGetter));

        private static readonly MethodInfo CallPropertyGetterByReferenceOpenGenericMethod =
            typeof(PropertyHelper).GetTypeInfo().GetDeclaredMethod(nameof(CallPropertyGetterByReference));

        private static readonly MethodInfo CallNullSafePropertyGetterOpenGenericMethod =
            typeof(PropertyHelper).GetTypeInfo().GetDeclaredMethod(nameof(CallNullSafePropertyGetter));

        private static readonly MethodInfo CallNullSafePropertyGetterByReferenceOpenGenericMethod =
            typeof(PropertyHelper).GetTypeInfo().GetDeclaredMethod(nameof(CallNullSafePropertyGetterByReference));

        private static readonly MethodInfo CallPropertySetterOpenGenericMethod =
            typeof(PropertyHelper).GetTypeInfo().GetDeclaredMethod(nameof(CallPropertySetter));

        // Using an array rather than IEnumerable, as target will be called on the hot path numerous times.
        private static readonly ConcurrentDictionary<Type, PropertyHelper[]> PropertiesCache =
            new();

        private static readonly ConcurrentDictionary<Type, PropertyHelper[]> VisiblePropertiesCache =
            new();

        private Action<object, object> _valueSetter;

        /// <summary>
        /// Initializes a fast <see cref="PropertyHelper"/>.
        /// This constructor does not cache the helper. For caching, use <see cref="GetProperties(object)"/>.
        /// </summary>
        public PropertyHelper(PropertyInfo property)
        {
            this.Property = property ?? throw new ArgumentNullException(nameof(property));
            this.Name = property.Name;
            this.ValueGetter = MakeFastPropertyGetter(property);
        }

        // Delegate type for a by-ref property getter
        private delegate TValue ByRefFunc<TDeclaringType, TValue>(ref TDeclaringType arg);

        /// <summary>
        /// Gets the backing <see cref="PropertyInfo"/>.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// Gets (or sets in derived types) the property name.
        /// </summary>
        public virtual string Name { get; protected set; }

        /// <summary>
        /// Gets the property value getter.
        /// </summary>
        public Func<object, object> ValueGetter { get; }

        /// <summary>
        /// Gets the property value setter.
        /// </summary>
        public Action<object, object> ValueSetter
        {
            get
            {
                this._valueSetter ??= MakeFastPropertySetter(this.Property);

                return this._valueSetter;
            }
        }

        /// <summary>
        /// Returns the property value for the specified <paramref name="instance"/>.
        /// </summary>
        /// <param name="instance">The object whose property value will be returned.</param>
        /// <returns>The property value.</returns>
        public object GetValue(object instance)
        {
            return this.ValueGetter(instance);
        }

        /// <summary>
        /// Sets the property value for the specified <paramref name="instance" />.
        /// </summary>
        /// <param name="instance">The object whose property value will be set.</param>
        /// <param name="value">The property value.</param>
        public void SetValue(object instance, object value)
        {
            this.ValueSetter(instance, value);
        }

        /// <summary>
        /// Creates and caches fast property helpers that expose getters for every public get property on the
        /// underlying type.
        /// </summary>
        /// <param name="instance">the instance to extract property accessors for.</param>
        /// <returns>a cached array of all public property getters from the underlying type of target instance.
        /// </returns>
        public static PropertyHelper[] GetProperties(object instance)
        {
            return GetProperties(instance.GetType());
        }

        /// <summary>
        /// Creates and caches fast property helpers that expose getters for every public get property on the
        /// specified type.
        /// </summary>
        /// <param name="type">the type to extract property accessors for.</param>
        /// <returns>a cached array of all public property getters from the type of target instance.
        /// </returns>
        public static PropertyHelper[] GetProperties(Type type)
        {
            return GetProperties(type, CreateInstance, PropertiesCache);
        }

        /// <summary>
        /// <para>
        /// Creates and caches fast property helpers that expose getters for every non-hidden get property
        /// on the specified type.
        /// </para>
        /// <para>
        /// hidden by definitions using the <c>new</c> keyword.
        /// </para>
        /// </summary>
        /// <param name="instance">The instance to extract property accessors for.</param>
        /// <returns>
        /// A cached array of all public property getters from the instance's type.
        /// </returns>
        public static PropertyHelper[] GetVisibleProperties(object instance)
        {
            return GetVisibleProperties(instance.GetType(), CreateInstance, PropertiesCache, VisiblePropertiesCache);
        }

        /// <summary>
        /// <para>
        /// Creates and caches fast property helpers that expose getters for every non-hidden get property
        /// on the specified type.
        /// </para>
        /// <para>
        /// hidden by definitions using the <c>new</c> keyword.
        /// </para>
        /// </summary>
        /// <param name="type">The type to extract property accessors for.</param>
        /// <returns>
        /// A cached array of all public property getters from the type.
        /// </returns>
        public static PropertyHelper[] GetVisibleProperties(Type type)
        {
            return GetVisibleProperties(type, CreateInstance, PropertiesCache, VisiblePropertiesCache);
        }

        protected static PropertyHelper[] GetVisibleProperties(
            Type type,
            Func<PropertyInfo, PropertyHelper> createPropertyHelper,
            ConcurrentDictionary<Type, PropertyHelper[]> allPropertiesCache,
            ConcurrentDictionary<Type, PropertyHelper[]> visiblePropertiesCache)
        {
            if (visiblePropertiesCache.TryGetValue(type, out PropertyHelper[] result))
            {
                return result;
            }

            // The simple and common case, this is normal POCO object - no need to allocate.
            bool allPropertiesDefinedOnType = true;
            PropertyHelper[] allProperties = GetProperties(type, createPropertyHelper, allPropertiesCache);
            foreach (PropertyHelper propertyHelper in allProperties)
            {
                if (propertyHelper.Property.DeclaringType != type)
                {
                    allPropertiesDefinedOnType = false;
                    break;
                }
            }

            if (allPropertiesDefinedOnType)
            {
                result = allProperties;
                visiblePropertiesCache.TryAdd(type, result);
                return result;
            }

            // There's some inherited properties here, so we need to check for hiding via 'new'.
            var filteredProperties = new List<PropertyHelper>(allProperties.Length);
            foreach (PropertyHelper propertyHelper in allProperties)
            {
                Type declaringType = propertyHelper.Property.DeclaringType;
                if (declaringType == type)
                {
                    filteredProperties.Add(propertyHelper);
                    continue;
                }

                // If this property was declared on a base type then look for the definition closest to the
                // the type to see if we should include it.
                bool ignoreProperty = false;

                // Walk up the hierarchy until we find the type that actally declares this
                // PropertyInfo.
                TypeInfo currentTypeInfo = type.GetTypeInfo();
                TypeInfo declaringTypeInfo = declaringType.GetTypeInfo();
                while (currentTypeInfo != null && currentTypeInfo != declaringTypeInfo)
                {
                    // We've found a 'more proximal' public definition
                    PropertyInfo declaredProperty = currentTypeInfo.GetDeclaredProperty(propertyHelper.Name);
                    if (declaredProperty != null)
                    {
                        ignoreProperty = true;
                        break;
                    }

                    currentTypeInfo = currentTypeInfo.BaseType?.GetTypeInfo();
                }

                if (!ignoreProperty)
                {
                    filteredProperties.Add(propertyHelper);
                }
            }

            result = filteredProperties.ToArray();
            visiblePropertiesCache.TryAdd(type, result);
            return result;
        }

        /// <summary>
        /// Creates a single fast property getter. The result is not cached.
        /// </summary>
        /// <param name="propertyInfo">propertyInfo to extract the getter for.</param>
        /// <returns>a fast getter.</returns>
        /// <remarks>
        /// This method is more memory efficient than a dynamically compiled lambda, and about the
        /// same speed.
        /// </remarks>
        public static Func<object, object> MakeFastPropertyGetter(PropertyInfo propertyInfo)
        {
            Debug.Assert(propertyInfo != null);

            return MakeFastPropertyGetter(
                propertyInfo,
                CallPropertyGetterOpenGenericMethod,
                CallPropertyGetterByReferenceOpenGenericMethod);
        }

        /// <summary>
        /// Creates a single fast property getter which is safe for a null input object. The result is not cached.
        /// </summary>
        /// <param name="propertyInfo">propertyInfo to extract the getter for.</param>
        /// <returns>a fast getter.</returns>
        /// <remarks>
        /// This method is more memory efficient than a dynamically compiled lambda, and about the
        /// same speed.
        /// </remarks>
        public static Func<object, object> MakeNullSafeFastPropertyGetter(PropertyInfo propertyInfo)
        {
            Debug.Assert(propertyInfo != null);

            return MakeFastPropertyGetter(
                propertyInfo,
                CallNullSafePropertyGetterOpenGenericMethod,
                CallNullSafePropertyGetterByReferenceOpenGenericMethod);
        }

        private static Func<object, object> MakeFastPropertyGetter(
            PropertyInfo propertyInfo,
            MethodInfo propertyGetterWrapperMethod,
            MethodInfo propertyGetterByRefWrapperMethod)
        {
            Debug.Assert(propertyInfo != null);

            // Must be a generic method with a Func<,> parameter
            Debug.Assert(propertyGetterWrapperMethod != null);
            Debug.Assert(propertyGetterWrapperMethod.IsGenericMethodDefinition);
            Debug.Assert(propertyGetterWrapperMethod.GetParameters().Length == 2);

            // Must be a generic method with a ByRefFunc<,> parameter
            Debug.Assert(propertyGetterByRefWrapperMethod != null);
            Debug.Assert(propertyGetterByRefWrapperMethod.IsGenericMethodDefinition);
            Debug.Assert(propertyGetterByRefWrapperMethod.GetParameters().Length == 2);

            MethodInfo getMethod = propertyInfo.GetMethod;
            Debug.Assert(getMethod != null);
            Debug.Assert(!getMethod.IsStatic);
            Debug.Assert(getMethod.GetParameters().Length == 0);

            // Instance methods in the CLR can be turned into static methods where the first parameter
            // is open over "target". This parameter is always passed by reference, so we have a code
            // path for value types and a code path for reference types.
            if (getMethod.DeclaringType.GetTypeInfo().IsValueType)
            {
                // Create a delegate (ref TDeclaringType) -> TValue
                return MakeFastPropertyGetter(
                    typeof(ByRefFunc<,>),
                    getMethod,
                    propertyGetterByRefWrapperMethod);
            }
            else
            {
                // Create a delegate TDeclaringType -> TValue
                return MakeFastPropertyGetter(
                    typeof(Func<,>),
                    getMethod,
                    propertyGetterWrapperMethod);
            }
        }

        private static Func<object, object> MakeFastPropertyGetter(
            Type openGenericDelegateType,
            MethodInfo propertyGetMethod,
            MethodInfo openGenericWrapperMethod)
        {
            Type typeInput = propertyGetMethod.DeclaringType;
            Type typeOutput = propertyGetMethod.ReturnType;

            Type delegateType = openGenericDelegateType.MakeGenericType(typeInput, typeOutput);
            Delegate propertyGetterDelegate = propertyGetMethod.CreateDelegate(delegateType);

            MethodInfo wrapperDelegateMethod = openGenericWrapperMethod.MakeGenericMethod(typeInput, typeOutput);
            Delegate accessorDelegate = wrapperDelegateMethod.CreateDelegate(
                typeof(Func<object, object>),
                propertyGetterDelegate);

            return (Func<object, object>)accessorDelegate;
        }

        /// <summary>
        /// Creates a single fast property setter for reference types. The result is not cached.
        /// </summary>
        /// <param name="propertyInfo">propertyInfo to extract the setter for.</param>
        /// <returns>a fast getter.</returns>
        /// <remarks>
        /// This method is more memory efficient than a dynamically compiled lambda, and about the
        /// same speed. This only works for reference types.
        /// </remarks>
        public static Action<object, object> MakeFastPropertySetter(PropertyInfo propertyInfo)
        {
            Debug.Assert(propertyInfo != null);
            Debug.Assert(!propertyInfo.DeclaringType.GetTypeInfo().IsValueType);

            MethodInfo setMethod = propertyInfo.SetMethod;
            Debug.Assert(setMethod != null);
            Debug.Assert(!setMethod.IsStatic);
            Debug.Assert(setMethod.ReturnType == typeof(void));
            ParameterInfo[] parameters = setMethod.GetParameters();
            Debug.Assert(parameters.Length == 1);

            // Instance methods in the CLR can be turned into static methods where the first parameter
            // is open over "target". This parameter is always passed by reference, so we have a code
            // path for value types and a code path for reference types.
            Type typeInput = setMethod.DeclaringType;
            Type parameterType = parameters[0].ParameterType;

            // Create a delegate TDeclaringType -> { TDeclaringType.Property = TValue; }
            Delegate propertySetterAsAction =
                setMethod.CreateDelegate(typeof(Action<,>).MakeGenericType(typeInput, parameterType));
            MethodInfo callPropertySetterClosedGenericMethod =
                CallPropertySetterOpenGenericMethod.MakeGenericMethod(typeInput, parameterType);
            Delegate callPropertySetterDelegate =
                callPropertySetterClosedGenericMethod.CreateDelegate(
                    typeof(Action<object, object>), propertySetterAsAction);

            return (Action<object, object>)callPropertySetterDelegate;
        }

        /// <summary>
        /// Given an object, adds each instance property with a public get method as a key and its
        /// associated value to a dictionary.
        /// If the object is already an <see cref="IDictionary{String, Object}"/> instance, then a copy
        /// is returned.
        /// </summary>
        /// <remarks>
        /// The implementation of PropertyHelper will cache the property accessors per-type. This is
        /// faster when the the same type is used multiple times with ObjectToDictionary.
        /// </remarks>
        /// <param name="value">The input object.</param>
        /// <returns>The dictionary representation of the object.</returns>
        public static IDictionary<string, object> ObjectToDictionary(object value)
        {
            var dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
            {
                return new Dictionary<string, object>(dictionary, StringComparer.OrdinalIgnoreCase);
            }

            dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            if (value != null)
            {
                foreach (PropertyHelper helper in GetProperties(value))
                {
                    dictionary[helper.Name] = helper.GetValue(value);
                }
            }

            return dictionary;
        }
#pragma warning disable IDE0019
        private static PropertyHelper CreateInstance(PropertyInfo property)
        {
            return new PropertyHelper(property);
        }

        // Called via reflection
        private static object CallPropertyGetter<TDeclaringType, TValue>(
            Func<TDeclaringType, TValue> getter,
            object target)
        {
            return getter((TDeclaringType)target);
        }

        // Called via reflection
        private static object CallPropertyGetterByReference<TDeclaringType, TValue>(
            ByRefFunc<TDeclaringType, TValue> getter,
            object target)
        {
            var unboxed = (TDeclaringType)target;
            return getter(ref unboxed);
        }

        // Called via reflection
        private static object CallNullSafePropertyGetter<TDeclaringType, TValue>(
            Func<TDeclaringType, TValue> getter,
            object target)
        {
            if (target == null)
            {
                return null;
            }

            return getter((TDeclaringType)target);
        }

        // Called via reflection
        private static object CallNullSafePropertyGetterByReference<TDeclaringType, TValue>(
            ByRefFunc<TDeclaringType, TValue> getter,
            object target)
        {
            if (target == null)
            {
                return null;
            }

            var unboxed = (TDeclaringType)target;
            return getter(ref unboxed);
        }

        private static void CallPropertySetter<TDeclaringType, TValue>(
            Action<TDeclaringType, TValue> setter,
            object target,
            object value)
        {
            setter((TDeclaringType)target, (TValue)value);
        }
        protected static PropertyHelper[] GetProperties(
                    Type type,
                    Func<PropertyInfo, PropertyHelper> createPropertyHelper,
                    ConcurrentDictionary<Type, PropertyHelper[]> cache)
        {
            // Unwrap nullable types. This means Nullable<T>.Value and Nullable<T>.HasValue will not be
            // part of the sequence of properties returned by this method.
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (!cache.TryGetValue(type, out PropertyHelper[] helpers))
            {
                // We avoid loading indexed properties using the Where statement.
                IEnumerable<PropertyInfo> properties = type.GetRuntimeProperties().Where(IsInterestingProperty);

                TypeInfo typeInfo = type.GetTypeInfo();
                if (typeInfo.IsInterface)
                {
                    // Reflection does not return information about inherited properties on the interface itself.
                    properties = properties.Concat(typeInfo.ImplementedInterfaces.SelectMany(
                        interfaceType => interfaceType.GetRuntimeProperties().Where(IsInterestingProperty)));
                }

                helpers = properties.Select(p => createPropertyHelper(p)).ToArray();
                cache.TryAdd(type, helpers);
            }

            return helpers;
        }
        // Indexed properties are not useful (or valid) for grabbing properties off an object.
        private static bool IsInterestingProperty(PropertyInfo property)
        {
            return property.GetIndexParameters().Length == 0 &&
                property.GetMethod != null &&
                property.GetMethod.IsPublic &&
                !property.GetMethod.IsStatic;
        }
    }
}
