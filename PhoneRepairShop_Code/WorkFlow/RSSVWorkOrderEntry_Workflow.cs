using PX.Data;
using PX.Data.BQL.Fluent;
using PX.Data.WorkflowAPI;
using PX.Objects.AR;
using PX.Objects.Common;
using static PX.Data.WorkflowAPI.BoundedTo<PhoneRepairShop.RSSVWorkOrderEntry,
    PhoneRepairShop.RSSVWorkOrder>;

namespace PhoneRepairShop
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
    public class RSSVWorkOrderEntry_Workflow : PXGraphExtension<RSSVWorkOrderEntry>
    {
        #region Constants
        //the states class is the one that defines the states of the workflow 
        public static class States
        {

            public const string OnHold = WorkOrderStatusConstants.OnHold;
            public const string ReadyForAssignment = WorkOrderStatusConstants.ReadyForAssignment;
            public const string PendingPayment = WorkOrderStatusConstants.PendingPayment;
            public const string Assigned = WorkOrderStatusConstants.Assigned;
            public const string Completed = WorkOrderStatusConstants.Completed;
            public const string Paid = WorkOrderStatusConstants.Paid;

            //Constant usable in BQL WHERE clauses example: Where<WorkOrder.status, Equal<States.onHold>>
            public class onHold : PX.Data.BQL.BqlString.Constant<onHold>
            {
                //Constructor of the nested BQL class onHold
                //It tells acumatica "Hey, when someone uses States.Hold in a Bql query, interpret as a constant string with the value onHold"
                //Where<WorkOrder.status, Equal<States.onHold>>
                public onHold() : base(OnHold) { }
            }

            public class readyForAssignment : PX.Data.BQL.BqlString.Constant<readyForAssignment>
            {
                public readyForAssignment() : base(ReadyForAssignment) { }
            }

            public class pendingPayment : PX.Data.BQL.BqlString.Constant<pendingPayment>
            {
                public pendingPayment() : base(PendingPayment) { }
            }

            public class assigned : PX.Data.BQL.BqlString.Constant<assigned>
            {
                public assigned() : base(Assigned) { }
            }

            public class completed : PX.Data.BQL.BqlString.Constant<completed>
            {
                public completed() : base(Completed) { }
            }

            public class paid : PX.Data.BQL.BqlString.Constant<paid>
            {
                public paid() : base(Paid) { }
            }
        }

        #endregion

        #region Condition
        protected static void Configure(WorkflowContext<RSSVWorkOrderEntry, RSSVWorkOrder> context)
        {
            // Create a from called FormAssign that asks the user to choose an assignee
            var formAssign = context.Forms.Create("FormAssign", form =>
            form.Prompt("Assign").WithFields(fields =>
            {
                fields.Add("Assignee", field => field
                      .WithSchemaOf<RSSVWorkOrder.assignee>()
                      .IsRequired()
                      .Prompt("Assignee"));
            }));
            #region Categories

            //Give me the standard menu sections used in action. I want the one called Processing
            var commonCategories = CommonActionCategories.Get(context);
            var processingCategory = commonCategories.Processing;
            #endregion

            // "Give me an access to all the custom rules i defined in my Conditions class."
            var conditions = context.Conditions.GetPack<Conditions>();

            //"For this screen, use the status field to track the workflow state, and heres the default process it should follow."
            //
            context.AddScreenConfigurationFor
            (screen => screen
                .StateIdentifierIs<RSSVWorkOrder.status>()
                .AddDefaultFlow
                (flow => flow
                    //"When the document is in this stage, heres how it should behave."
                    .WithFlowStates
                    (flowStates =>
                        {
                            flowStates.Add<States.onHold>(flowState =>
                            GetOnHoldBehavior(flowState));
                            flowStates.Add<States.readyForAssignment>(flowState =>
                            GetReadyForAssignmentBehavior(flowState));
                            flowStates.Add<States.pendingPayment>(flowState =>
                            GetPendingPaymentBehaviour(flowState));
                            flowStates.Add<States.assigned>(flowState =>
                            GetAssignedBehaviour(flowState));
                            flowStates.Add<States.completed>(flowState =>
                            GetCompletedBehavior(flowState));
                            flowStates.Add<States.paid>(flowState =>
                           GetPaidBehavior(flowState));
                        }
                    )

                    //.WithTransitions
                    //(transitions =>
                    //    {
                    //        transitions.Add(transition => transition
                    //            .From<States.onHold>().To<States.readyForAssignment>()
                    //            .IsTriggeredOn(graph => graph.ReleaseFromHold));
                    //    }
                    //)

                    //"When this action happens, move to this new state"
                    .WithTransitions(transitions =>
                    {
                        transitions.AddGroupFrom<States.onHold>(transitionGroup =>
                        {
                            transitionGroup.Add(transition =>
                                transition.To<States.readyForAssignment>()
                                .IsTriggeredOn(graph => graph.ReleaseFromHold)
                                .When(!conditions.RequiresPrepayment));
                            transitionGroup.Add(transition =>
                                transition.To<States.pendingPayment>()
                                .IsTriggeredOn(graph => graph.ReleaseFromHold)
                                .When(conditions.RequiresPrepayment));
                        });
                        transitions.AddGroupFrom<States.readyForAssignment>(transitionGroup =>
                        {
                            transitionGroup.Add(transition =>
                                transition.To<States.onHold>()
                                .IsTriggeredOn(graph => graph.PutOnHold));
                            transitionGroup.Add(transition =>
                               transition.To<States.assigned>()
                               .IsTriggeredOn(graph => graph.Assign));
                        });
                        transitions.AddGroupFrom<States.pendingPayment>(transitionGroup =>
                        {
                            transitionGroup.Add(transition =>
                                transition.To<States.onHold>()
                                .IsTriggeredOn(graph => graph.PutOnHold));
                            transitionGroup.Add(transition =>
                                 transition.To<States.readyForAssignment>()
                                 .IsTriggeredOn(graph => graph.OnInvoiceGotPrepaid));
                        });
                        transitions.AddGroupFrom<States.assigned>(transitionGroup =>
                        {
                            transitionGroup.Add(transition =>
                                transition.To<States.completed>()
                                .IsTriggeredOn(graph => graph.Complete));
                        });
                        transitions.AddGroupFrom<States.completed>(transitionGroup =>
                        {
                            transitionGroup.Add(transition =>
                                transition.To<States.paid>()
                                .IsTriggeredOn(graph => graph.OnCloseDocument));
                        });
                    })
                )
                //Hey system, when this specific thing happens, do this specific task.
                .WithHandlers(handlers =>
                {
                    handlers.Add(handler => handler
                        .WithTargetOf<ARInvoice>()
                        .OfEntityEvent<ARInvoice.Events>(
                        workflowEvent => workflowEvent.CloseDocument)
                        .Is(graph => graph.OnCloseDocument)
                        .UsesPrimaryEntityGetter<
                        SelectFrom<RSSVWorkOrder>.
                        Where<RSSVWorkOrder.invoiceNbr 
                        .IsEqual<ARRegister.refNbr.FromCurrent>>>());
                    handlers.Add(handler => handler
                         .WithTargetOf<ARRegister>()
                         .OfEntityEvent<RSSVWorkOrder.WorkflowEvents>(
                         workflowEvent => workflowEvent.InvoiceGotPrepaid)
                         .Is(graph => graph.OnInvoiceGotPrepaid) 
                         .UsesPrimaryEntityGetter<
                         SelectFrom<RSSVWorkOrder>.
                         Where<RSSVWorkOrder.invoiceNbr
                         .IsEqual<ARRegister.refNbr.FromCurrent>>>());
                })

                //WithCategories is used to add categories to the more menu UI
                //"Put my actions into the processing section of the screen menu."
                .WithCategories
                (categories =>
                    {
                        categories.Add(processingCategory);
                    }
                )
                //"Here are the buttons i want to show on the screen, and what they should do."
                .WithActions
                (actions =>
                    {
                        actions.Add(graph => graph.ReleaseFromHold,
                           action => action.WithCategory(processingCategory));
                        actions.Add(graph => graph.PutOnHold,
                           action => action.WithCategory(processingCategory));
                        actions.Add(graph => graph.Assign,
                           action => action.WithCategory(processingCategory)
                          .WithForm(formAssign)
                          .WithFieldAssignments(fields =>
                          {
                              fields.Add<RSSVWorkOrder.assignee>(field =>
                              field.SetFromFormField(formAssign, "Assignee"));
                          }));
                        actions.Add(graph => graph.Complete, action => action
                         .WithCategory(processingCategory, Placement.Last)
                         .WithFieldAssignments(fields => fields
                         .Add<RSSVWorkOrder.dateCompleted>(field =>
                         field.SetFromToday())));
                        actions.Add(graph => graph.CreateInvoiceAction,
                            action => action.WithCategory(processingCategory)
                        .IsDisabledWhen(conditions.HasInvoice));
                    }
                )
                //"When the document enters this workflow state, show a form called FormAssign"
                .WithForms(forms => forms.Add(formAssign))
            );
        }

        #region Conditions
        public class Conditions : Condition.Pack
        {
            //GetPrCreate is used to create a condition that can be used in the workflow
            public Condition RequiresPrepayment => GetOrCreate(condition =>
             condition.FromBql<Where<RSSVRepairService.prepayment
             .FromSelectorOf<RSSVWorkOrder.serviceID>.IsEqual<True>>>());
            public Condition HasInvoice => GetOrCreate(condition =>
                condition.FromBql<Where<RSSVWorkOrder.invoiceNbr.IsNotNull>>());
        }
        #endregion

        #endregion

        //"Set up the workflow for the RSSVWorkOrder screen using its configuration context."
        public sealed override void Configure(PXScreenConfiguration config)
        {
            Configure(config.GetScreenConfigurationContext<RSSVWorkOrderEntry,
            RSSVWorkOrder>());
        }

        #region Workflow States

        private static BaseFlowStep.IConfigured GetPaidBehavior(
           FlowState.INeedAnyFlowStateConfig flowState)
        {
            return flowState
            .WithFieldStates(states =>
            {
                states.AddField<RSSVWorkOrder.customerID>(state
                    => state.IsDisabled());
                states.AddField<RSSVWorkOrder.serviceID>(state
                    => state.IsDisabled());
                states.AddField<RSSVWorkOrder.deviceID>(state
                    => state.IsDisabled());
            });
        }
        private static BaseFlowStep.IConfigured GetCompletedBehavior(FlowState.INeedAnyFlowStateConfig flowState)
        {
            return flowState
            .WithFieldStates(states =>
            {
                states.AddField<RSSVWorkOrder.customerID>(state
                 => state.IsDisabled());
                states.AddField<RSSVWorkOrder.serviceID>(state
                 => state.IsDisabled());
                states.AddField<RSSVWorkOrder.deviceID>(state
                 => state.IsDisabled());
            })
            .WithActions(actions =>
            {
                actions.Add(graph => graph.CreateInvoiceAction,
                action => action.IsDuplicatedInToolbar()
                .WithConnotation(ActionConnotation.Success));
            })
            .WithEventHandlers(handlers =>
            {
                handlers.Add(graph => graph.OnCloseDocument);
            });
        }

        private static BaseFlowStep.IConfigured GetAssignedBehaviour(FlowState.INeedAnyFlowStateConfig flowState)
        {
            //"When the document is in this workflow state, lock these fields so users cant change them."
            return flowState
            .WithFieldStates(states =>
            {
                states.AddField<RSSVWorkOrder.customerID>(state
                => state.IsDisabled());
                states.AddField<RSSVWorkOrder.serviceID>(state
                => state.IsDisabled());
                states.AddField<RSSVWorkOrder.deviceID>(state
                => state.IsDisabled());
            })
           .WithActions(actions =>
            {
                actions.Add(graph => graph.Complete, action => action
                .IsDuplicatedInToolbar()
                .WithConnotation(ActionConnotation.Success));
            });
        }
        private static BaseFlowStep.IConfigured GetPendingPaymentBehaviour(FlowState.INeedAnyFlowStateConfig flowState)
        {
            //"When the document is in this workflow state, lock these fields so users cant change them."
            return flowState
            .WithFieldStates(states =>
            {
                states.AddField<RSSVWorkOrder.customerID>(state
                => state.IsDisabled());
                states.AddField<RSSVWorkOrder.serviceID>(state
                => state.IsDisabled());
                states.AddField<RSSVWorkOrder.deviceID>(state
                => state.IsDisabled());
            })
            //Add a Put On hold button to the screen, and make sure it shows up in the Toolbar for quick access.
            .WithActions(actions =>
             {
                 actions.Add(graph => graph.PutOnHold,
                 action => action.IsDuplicatedInToolbar());
             })
            .WithActions(actions =>
             {
                 actions.Add(graph => graph.CreateInvoiceAction,
                 action => action.IsDuplicatedInToolbar()
                 .WithConnotation(ActionConnotation.Success));
             })
            .WithEventHandlers(handlers =>
             {
                 handlers.Add(graph => graph.OnInvoiceGotPrepaid);
             });
        }
        private static BaseFlowStep.IConfigured GetOnHoldBehavior(
        FlowState.INeedAnyFlowStateConfig flowState)
        {
            return flowState
            //"This is the first state when the document enters the workflow."
            .IsInitial()
            //Here are the buttons users can click in this state.
            .WithActions(actions =>
            {
                //Add a button called ReleaseFromHold
                actions.Add(graph => graph.ReleaseFromHold,
                //Show this button in the toolbar and also in the more menu
                 action => action.IsDuplicatedInToolbar()
                 //Make the button look positive - like green or success-themed.
                 .WithConnotation(
                 ActionConnotation.Success));
            });
        }

        private static BaseFlowStep.IConfigured GetReadyForAssignmentBehavior(FlowState.INeedAnyFlowStateConfig flowState)
        {
            return flowState
            .WithFieldStates(states =>
            {
                states.AddField<RSSVWorkOrder.customerID>(state
                => state.IsDisabled());
                states.AddField<RSSVWorkOrder.serviceID>(state
                => state.IsDisabled());
                states.AddField<RSSVWorkOrder.deviceID>(state
                => state.IsDisabled());
            })
            .WithActions(actions =>
            {
                actions.Add(graph => graph.PutOnHold,
                   action => action.IsDuplicatedInToolbar());
                actions.Add(graph => graph.Assign,
                   action => action.IsDuplicatedInToolbar()
                   .WithConnotation(ActionConnotation.Success));
            });
        }
        #endregion
    }
}
