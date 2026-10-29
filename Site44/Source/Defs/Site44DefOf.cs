using RimWorld;
using Verse;

namespace Site44
{
    [DefOf]
    public static class Site44DefOf
    {
        public static ThingDef Site44_BlastWall;
        public static ThingDef Site44_ViewportWall;
        public static ThingDef Site44_SealedHatch;
        public static ThingDef Site44_Terminal;
        public static ThingDef Site44_Stele;
        public static ThingDef Site44_SupplyCrate;
        public static ThingDef Site44_DiagnosticNode;
        public static ThingDef Site44_CoolantValve;
        public static ThingDef Site44_ReactorConsole;
        public static ThingDef Site44_SecurityTurret;
        public static ThingDef Site44_LampEmergency;
        public static ThingDef Site44_LampBright;
        public static ThingDef Site44_EVASuit;
        public static ThingDef Site44_EVAHelmet;
        public static JobDef Site44_HackTerminal;
        public static JobDef Site44_UseGate;
        public static ThingDef Site44_ExtractionGate;
        public static SitePartDef Site44_Ruin;
        public static QuestScriptDef OpportunitySite_Site44Ruin;
        public static FactionDef Site44_Facility;
#if ENCLAVE
        public static FactionDef Site44_Enclave;
        public static SitePartDef Site44_EnclaveSitePart;
        public static QuestScriptDef OpportunitySite_Site44Enclave;
#endif

        static Site44DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(Site44DefOf));
        }
    }
}
