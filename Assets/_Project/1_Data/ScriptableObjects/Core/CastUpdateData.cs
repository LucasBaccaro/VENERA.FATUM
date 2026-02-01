namespace Genesis.Data {
    /// <summary>
    /// Datos para la actualización de la barra de casteo.
    /// Ubicado en Genesis.Data para evitar dependencias circulares.
    /// </summary>
    public struct CastUpdateData {
        public float Percent;
        public string AbilityName;
        public float RemainingTime;
        public float Duration;
        public bool IsChanneling;
        public float TickRate;
        public AbilityCategory Category;

        public static CastUpdateData Empty => new CastUpdateData {
            Percent = 0,
            AbilityName = "",
            RemainingTime = 0,
            Duration = 0,
            IsChanneling = false,
            TickRate = 0,
            Category = AbilityCategory.Magical
        };
    }
}
