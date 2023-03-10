using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FireControl;

public class FireControlMod : Mod
{
    private readonly Dictionary<string, int> entryIndices = new();
    private readonly HashSet<string> hasEntry = new();
    private readonly string name;
    private readonly List<ThingDef> possibleTurrets = new();
    private readonly HashSet<string> present = new();
    private string[] buffers;
    private List<FireControlSettings.Entry> oldEntries;
    private Vector2 scrollPos;

    private FireControlSettings settings;

    public FireControlMod(ModContentPack content) : base(content)
    {
        name = content.Name;
        LongEventHandler.ExecuteWhenFinished(delegate
        {
            LoadSettings();
            AutoPatch();
        });
    }

    private FireControlSettings.Entry EntryFor(string name) =>
        entryIndices.ContainsKey(name) ? settings.entries[entryIndices[name]] : new FireControlSettings.Entry(name);

    private FireControlSettings.Entry EntryFor(Def def) => EntryFor(def.defName);

    private void LoadSettings()
    {
        settings = GetSettings<FireControlSettings>();
        hasEntry.AddRange(settings.entries.Select(e => e.defName));
        for (var i = 0; i < settings.entries.Count; i++) entryIndices.Add(settings.entries[i].defName, i);
        buffers = new string[settings.entries.Count];
        oldEntries = settings.entries.ListFullCopy();
    }

    public override string SettingsCategory() => name;

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        var viewRect = new Rect(0, 0, inRect.width - 20f, present.Count * (25 * 3 + 10));
        Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);
        for (var i = 0; i < settings.entries.Count; i++)
        {
            var entry = settings.entries[i];
            if (!present.Contains(entry.defName)) continue;
            var def = ThingDef.Named(entry.defName);
            var rect = viewRect.TakeTopPart(25 * 3 + 10);
            rect.TakeBottomPart(10f).DrawLineAcross();
            GUI.DrawTexture(new Rect(0, rect.y + 5, 40, 40).CenteredOnXIn(rect.TakeLeftPart(60f)), def.uiIcon);
            Widgets.Label(rect.TakeTopPart(25f), def.LabelCap);
            Widgets.CheckboxLabeled(rect.TakeTopPart(25f), "FireControl.Controllable".Translate(), ref entry.controllable);
            if (entry.controllable)
            {
                Widgets.Label(rect.LeftHalf(), "FireControl.Complexity".Translate());
                Widgets.IntEntry(rect.RightHalf(), ref entry.complexity, ref buffers[i]);
            }

            settings.entries[i] = entry;
        }

        var listing = new Listing_Standard();
        listing.Begin(viewRect);
        foreach (var possibleTurret in possibleTurrets)
        {
            if (hasEntry.Contains(possibleTurret.defName)) continue;
            if (listing.ButtonTextLabeled(possibleTurret.LabelCap, "FireControl.Add".Translate()))
            {
                settings.entries.Add(new FireControlSettings.Entry(possibleTurret.defName));
                hasEntry.Add(possibleTurret.defName);
                Array.Resize(ref buffers, settings.entries.Count);
            }

            listing.GapLine(10f);
        }

        listing.End();
        Widgets.EndScrollView();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        if (!oldEntries.SequenceEqual(settings.entries))
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("FireControl.Restart.Desc".Translate(), GenCommandLine.Restart, true,
                "FireControl.Restart".Translate()));
        oldEntries = settings.entries.ListFullCopy();
    }

    private void AutoPatch()
    {
        foreach (var def in DefDatabase<ThingDef>.AllDefs)
        {
            if (typeof(Building_Turret).IsAssignableFrom(def.thingClass))
            {
                possibleTurrets.Add(def);
                present.Add(def.defName);
                var entry = EntryFor(def);
                if (!entry.controllable) continue;
                for (var i = 0; i < def.comps.Count; i++)
                    if (def.comps[i] is CompProperties_Mannable mannable)
                    {
                        def.comps[i] = new CompProperties_AutoMannable
                        {
                            compClass = typeof(CompAutoMannable),
                            complexity = entry.complexity,
                            manuallyManable = true,
                            manWorkType = mannable.manWorkType
                        };
                        goto outer;
                    }

                def.comps.Add(new CompProperties_AutoMannable
                {
                    compClass = typeof(CompAutoMannable),
                    complexity = entry.complexity,
                    manuallyManable = false
                });
            }

        outer: ;
        }
    }
}

// ReSharper disable InconsistentNaming
public class FireControlSettings : ModSettings
{
    public List<Entry> entries = new();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
    }

    public struct Entry : IExposable
    {
        public string defName = "";
        public int complexity = 1;

        public bool controllable = true;

        public Entry(string name) => defName = name;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, nameof(defName));
            Scribe_Values.Look(ref complexity, nameof(complexity));
            Scribe_Values.Look(ref controllable, nameof(controllable));
        }
    }
}
