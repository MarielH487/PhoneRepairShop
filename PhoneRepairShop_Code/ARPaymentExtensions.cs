using PX.Data;
using System;

namespace PX.Objects.AR
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
    public sealed class ARRegisterExt : PXCacheExtension<PX.Objects.AR.ARRegister>
    {
        #region UsrPrepaymentPercent
        [PXDBDecimal]
        [PXDefault(TypeCode.Decimal, "0.0",
            PersistingCheck = PXPersistingCheck.Nothing)]
        [PXUIField(DisplayName = "Prepayment Percent")]

        public Decimal? UsrPrepaymentPercent { get; set; }
        public abstract class usrPrepaymentPercent : PX.Data.BQL.BqlDecimal.Field<usrPrepaymentPercent> { }
        #endregion
    }
}