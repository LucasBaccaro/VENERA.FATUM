namespace Genesis.Data {

    public enum DamageResultType {
        Normal,
        Critical,
        Overpower,
        CriticalHeal,
        Blocked,
        Evaded
    }

    public struct CombatResult {
        public float FinalDamage;
        public DamageResultType ResultType;
        public bool WasCritical;
        public bool WasOverpower;
        public float LifeStealAmount;

        public static CombatResult None => new CombatResult {
            FinalDamage = 0f,
            ResultType = DamageResultType.Normal,
            WasCritical = false,
            WasOverpower = false,
            LifeStealAmount = 0f
        };
    }
}
