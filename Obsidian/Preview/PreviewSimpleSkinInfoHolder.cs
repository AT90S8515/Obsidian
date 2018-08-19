using Fantome.Libraries.League.IO.WAD;
using System.Collections.Generic;

namespace Obsidian.Preview
{
    public class PreviewSimpleSkinInfoHolder
    {
        public WADEntry SKNEntry { get; }
        public WADEntry SKLEntry { get; }
        public Dictionary<string, WADEntry> Textures { get; }

        public PreviewSimpleSkinInfoHolder(WADEntry sknEntry, WADEntry sklEntry, Dictionary<string, WADEntry> textureEntries)
        {
            this.SKNEntry = sknEntry;
            this.SKLEntry = sklEntry;
            this.Textures = textureEntries;
        }
    }
}