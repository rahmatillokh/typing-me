using System;
using UnityEngine;

namespace TypingMe.Data
{
    /// <summary>
    /// Everything shown on the Author tab. Kept in an asset so it can be edited without touching
    /// code, and so nothing about a real person is hard-coded into the build.
    /// </summary>
    /// <remarks>
    /// Ships blank on purpose. <see cref="TypingMe.UI.AuthorUI"/> shows an explicit "not filled in"
    /// notice for any empty field rather than rendering a gap, and hides link buttons whose URL is
    /// missing — a dead button is worse than no button.
    /// </remarks>
    [CreateAssetMenu(menuName = "Typing Me/Author Info", fileName = "AuthorInfo")]
    public sealed class AuthorInfoSO : ScriptableObject
    {
        [Serializable]
        public sealed class Link
        {
            [Tooltip("Shown on the button, e.g. GITHUB or TELEGRAM.")]
            public string label;

            [Tooltip("Opened in the system browser. Include the scheme, e.g. https://…")]
            public string url;
        }

        [Header("Identity")]
        public string displayName;

        [Tooltip("One line under the name, e.g. 'Developer · Tashkent'.")]
        public string role;

        [Header("About")]
        [TextArea(3, 10)] public string bio;

        [Header("Links")]
        public Link[] links = Array.Empty<Link>();

        [Header("Support")]
        public string donateLabel = "SUPPORT THE PROJECT";

        [Tooltip("Donation page. The button stays hidden while this is empty.")]
        public string donateUrl;

        public bool HasIdentity => !string.IsNullOrWhiteSpace(displayName);
        public bool HasBio => !string.IsNullOrWhiteSpace(bio);
        public bool HasDonate => !string.IsNullOrWhiteSpace(donateUrl);
    }
}
