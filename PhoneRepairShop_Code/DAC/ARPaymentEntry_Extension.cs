﻿using PhoneRepairShop;
using PX.Data;
using PX.Data.BQL.Fluent;
using PX.Objects.AR;
using PX.Objects.CN.Compliance.AR.CacheExtensions;

namespace PhoneRepairShop_Code.DAC
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
    public class ARPaymentEntry_Extension : PXGraphExtension<ARPaymentEntry>
    {
        public virtual void _(Events.FieldDefaulting<ARPayment,
        ARRegisterExt.usrPrepaymentPercent> e)
        {
            RSSVSetup setupRecord = SelectFrom<RSSVSetup>.View.Select(Base);
            if (setupRecord != null)
            {
                e.NewValue = setupRecord.PrepaymentPercent;
            }
        }
    }
}
