using System;
using System.Activities;
using UiPath.CodedWorkflows;
using UiPath.CodedWorkflows.Utils;
using System.Runtime;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction;
using ExtractInvoiceData;

namespace ExtractInvoiceData
{
    [System.ComponentModel.Browsable(false)]
    public class ShapeInvoiceActivity : System.Activities.Activity
    {
        public InArgument<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.IDocumentData<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.DictionaryData>> in_Data { get; set; }
        public OutArgument<string> out_InvoiceJSON { get; set; }
        public OutArgument<string> out_PONumber { get; set; }
        public OutArgument<string> out_InvoiceNumber { get; set; }

        public ShapeInvoiceActivity()
        {
            this.Implementation = () =>
            {
                return new ShapeInvoiceActivityChild()
                {
                    in_Data = (this.in_Data == null ? (InArgument<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.IDocumentData<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.DictionaryData>>)Argument.CreateReference((Argument)new InArgument<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.IDocumentData<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.DictionaryData>>(), "in_Data") : (InArgument<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.IDocumentData<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.DictionaryData>>)Argument.CreateReference((Argument)this.in_Data, "in_Data")),
                    out_InvoiceJSON = (this.out_InvoiceJSON == null ? (OutArgument<string>)Argument.CreateReference((Argument)new OutArgument<string>(), "out_InvoiceJSON") : (OutArgument<string>)Argument.CreateReference((Argument)this.out_InvoiceJSON, "out_InvoiceJSON")),
                    out_PONumber = (this.out_PONumber == null ? (OutArgument<string>)Argument.CreateReference((Argument)new OutArgument<string>(), "out_PONumber") : (OutArgument<string>)Argument.CreateReference((Argument)this.out_PONumber, "out_PONumber")),
                    out_InvoiceNumber = (this.out_InvoiceNumber == null ? (OutArgument<string>)Argument.CreateReference((Argument)new OutArgument<string>(), "out_InvoiceNumber") : (OutArgument<string>)Argument.CreateReference((Argument)this.out_InvoiceNumber, "out_InvoiceNumber")),
                };
            };
        }
    }

    internal class ShapeInvoiceActivityChild : UiPath.CodedWorkflows.AsyncTaskCodedWorkflowActivity
    {
        public InArgument<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.IDocumentData<global::UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction.DictionaryData>> in_Data { get; set; }
        public OutArgument<string> out_InvoiceJSON { get; set; }
        public OutArgument<string> out_PONumber { get; set; }
        public OutArgument<string> out_InvoiceNumber { get; set; }
        public System.Collections.Generic.IDictionary<string, object> newResult { get; set; }

        public ShapeInvoiceActivityChild()
        {
            DisplayName = "ShapeInvoice";
        }

        protected override async System.Threading.Tasks.Task<Action<AsyncCodeActivityContext>> ExecuteAsync(AsyncCodeActivityContext context, System.Threading.CancellationToken cancellationToken)
        {
            var var_in_Data = in_Data.Get(context);
            var codedWorkflow = new global::ExtractInvoiceData.ShapeInvoice();
            CodedWorkflowHelper.Initialize(codedWorkflow, new UiPath.CodedWorkflows.Utils.CodedWorkflowsFeatureChecker(new System.Collections.Generic.List<string>() { UiPath.CodedWorkflows.Utils.CodedWorkflowsFeatures.AsyncEntrypoints }), context);
            var result = await System.Threading.Tasks.Task.Run(() => CodedWorkflowHelper.RunWithExceptionHandlingAsync(() =>
            {
                if (codedWorkflow is IBeforeAfterRun codedWorkflowWithBeforeAfter)
                {
                    codedWorkflowWithBeforeAfter.Before(new BeforeRunContext() { RelativeFilePath = "ShapeInvoice.cs" });
                }

                return System.Threading.Tasks.Task.CompletedTask;
            }, () =>
            {
                CodedExecutionHelper.Run(() =>
                {
                    {
                        var result = codedWorkflow.Execute(var_in_Data);
                        newResult = new System.Collections.Generic.Dictionary<string, object>
                        {
                            {
                                "out_InvoiceJSON",
                                result.out_InvoiceJSON
                            },
                            {
                                "out_PONumber",
                                result.out_PONumber
                            },
                            {
                                "out_InvoiceNumber",
                                result.out_InvoiceNumber
                            },
                        };
                    }
                }, cancellationToken);
                return System.Threading.Tasks.Task.FromResult(newResult);
            }, (exception, outArgs) =>
            {
                if (codedWorkflow is IBeforeAfterRun codedWorkflowWithBeforeAfter)
                {
                    codedWorkflowWithBeforeAfter.After(new AfterRunContext() { RelativeFilePath = "ShapeInvoice.cs", Exception = exception });
                }

                return System.Threading.Tasks.Task.CompletedTask;
            }), cancellationToken);
            return endContext =>
            {
                out_InvoiceJSON.Set(endContext, (string)result["out_InvoiceJSON"]);
                out_PONumber.Set(endContext, (string)result["out_PONumber"]);
                out_InvoiceNumber.Set(endContext, (string)result["out_InvoiceNumber"]);
            };
        }
    }
}