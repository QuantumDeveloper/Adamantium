using System;
using Adamantium.Multiverse.Input;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Universes;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class UniverseKeyTranslationTests
{
    [Test]
    public void EveryUIKeyNamedLikeAUniverseKey_TranslatesToIt()
    {
        foreach (var key in Enum.GetValues<Key>())
        {
            if (!Enum.TryParse<Keys>(key.ToString(), out var same))
            {
                continue;
            }

            Assert.That(UIUniverseOutput.TranslationKeys.TryGetValue(key, out var translated), Is.True, key.ToString());
            Assert.That(translated, Is.EqualTo(same), key.ToString());
        }
    }
}
