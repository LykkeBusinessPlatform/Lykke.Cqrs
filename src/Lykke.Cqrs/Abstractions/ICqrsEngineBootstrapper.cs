namespace Lykke.Cqrs
{
    /// <summary>
    /// Interface for cqrs engine bootstrapping.
    /// </summary>
    public interface ICqrsEngineBootstrapper
    {
        /// <summary>
        /// Starts bootstrapping.
        /// </summary>
        void Start();
    }
}