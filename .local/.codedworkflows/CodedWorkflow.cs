using System;
using System.Collections.Generic;
using System.Data;
using UiPath.Activities.System.Jobs.Coded;
using UiPath.CodedWorkflows;
using UiPath.Core;
using UiPath.Core.Activities.Storage;
using UiPath.Excel;
using UiPath.Excel.Activities;
using UiPath.Excel.Activities.API;
using UiPath.Excel.Activities.API.Models;
using UiPath.Orchestrator.Client.Models;

namespace RetrieveInvoiceDocument
{
    public partial class CodedWorkflow : CodedWorkflowBase
    {
        private Lazy<global::RetrieveInvoiceDocument.WorkflowRunnerService> _workflowRunnerServiceLazy;
        private Lazy<ConnectionsManager> _connectionsManagerLazy;
        public CodedWorkflow()
        {
            _ = new System.Type[]
            {
                typeof(UiPath.Core.Activities.API.ISystemService),
                typeof(UiPath.Excel.Activities.API.IExcelService)
            };
            _workflowRunnerServiceLazy = new Lazy<global::RetrieveInvoiceDocument.WorkflowRunnerService>(() => new global::RetrieveInvoiceDocument.WorkflowRunnerService(this.services));
#pragma warning disable
            _connectionsManagerLazy = new Lazy<ConnectionsManager>(() => new ConnectionsManager(serviceContainer));
#pragma warning restore
        }

        protected global::RetrieveInvoiceDocument.WorkflowRunnerService workflows => _workflowRunnerServiceLazy.Value;
        protected ConnectionsManager connections => _connectionsManagerLazy.Value;
#pragma warning disable
        protected UiPath.Excel.Activities.API.IExcelService excel { get => serviceContainer.Resolve<UiPath.Excel.Activities.API.IExcelService>() ; }
#pragma warning restore
#pragma warning disable
        protected UiPath.Core.Activities.API.ISystemService system { get => serviceContainer.Resolve<UiPath.Core.Activities.API.ISystemService>() ; }
#pragma warning restore
    }
}