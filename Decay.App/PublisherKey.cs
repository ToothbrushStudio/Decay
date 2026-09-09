namespace Decay.App
{
    /// <summary>
    /// The publisher's license-verification public key. Intentionally empty
    /// in the repository: run `dotnet run gen` in tools/Decay.KeyGen once,
    /// keep the private key OFFLINE, and paste the printed public key here
    /// before shipping. While empty, no license can activate (Free-only
    /// builds), and nothing else in the app is affected — decryption never
    /// needs a license.
    /// </summary>
    public static class PublisherKey
    {
        public const string SubjectPublicKeyInfoBase64 = "";
    }
}
