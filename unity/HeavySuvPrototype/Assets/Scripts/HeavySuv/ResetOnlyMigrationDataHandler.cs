using Unity.Services.Multiplayer;

namespace HeavySuvPrototype
{
    public sealed class ResetOnlyMigrationDataHandler : IMigrationDataHandler
    {
        public byte[] Generate()
        {
            return new byte[] { 1 };
        }

        public void Apply(byte[] migrationData)
        {
        }
    }
}
