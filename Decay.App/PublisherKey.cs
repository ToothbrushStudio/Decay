namespace Decay.App
{
    /// <summary>
    /// The publisher's license-verification public key, generated with
    /// tools/Decay.KeyGen. The matching private key is kept OFFLINE and is
    /// never part of the repository or a build. If this were empty, no
    /// license could activate (Free-only builds), and nothing else in the app
    /// would be affected — decryption never needs a license.
    /// </summary>
    public static class PublisherKey
    {
        public const string SubjectPublicKeyInfoBase64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEsFkJXT82JRx57NoTFZdO2/Jzyk4LDso4bi7lkb65rQdjey5W5qD9xTw28a0eHY5nAqnhlRi7Fa8P93+wcDTQVw==";
    }
}
