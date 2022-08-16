// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common
{
    public class Invoice
    {
        public int InvoiceID { get; set; }

        public int CustomerID { get; set; }

        public int BillToCustomerID { get; set; }

        public int OrderID { get; set; }

        public int DeliveryMethodID { get; set; }

        public int ContactPersonID { get; set; }

        public int AccountsPersonID { get; set; }

        public int SalespersonPersonID { get; set; }

        public int PackedByPersonID { get; set; }

        public string InvoiceDate { get; set; }

        public string CustomerPurchaseOrderNumber { get; set; }

        public bool IsCreditNote { get; set; }

        public string CreditNoteReason { get; set; }

        public string Comments { get; set; }

        public string DeliveryInstructions { get; set; }

        public string InternalComments { get; set; }

        public int TotalDryItems { get; set; }

        public int TotalChillerItems { get; set; }

        public string DeliveryRun { get; set; }

        public string RunPosition { get; set; }

        public string ReturnedDeliveryData { get; set; }

        public DateTime ConfirmedDeliveryTime { get; set; }

        public string ConfirmedReceivedBy { get; set; }

        public int LastEditedBy { get; set; }

        public DateTime LastEditedWhen { get; set; }
    }
}
