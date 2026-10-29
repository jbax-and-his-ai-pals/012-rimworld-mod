using System.Collections.Generic;
using Verse;

namespace Site44
{
    // One document in a thread: written out in full, never assembled from parts.
    public class StoryEntry
    {
        public string kind;     // "Memo", "Incident report", "System log" ...
        public string header;   // sender, recipient, date: whatever that kind of document opens with
        public string title;
        public string body;
    }

    // A short run of related documents that a complex's diagnostic terminals reveal in order. Each thread is its own piece of
    // writing with its own cast; threads that suit a kind of complex list it, the rest can turn up anywhere.
    public class StoryThreadDef : Def
    {
        public List<string> siteTypes = new List<string>();   // def names of Site44.SiteTypeDef; empty = any complex
        public List<StoryEntry> entries = new List<StoryEntry>();
    }
}
