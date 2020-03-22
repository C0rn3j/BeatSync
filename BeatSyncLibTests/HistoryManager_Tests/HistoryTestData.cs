﻿using BeatSyncLib.History;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSyncLibTests.HistoryManager_Tests
{
    public static class HistoryTestData
    {
        public static ReadOnlyDictionary<string, HistoryEntry> TestCollection1 = new ReadOnlyDictionary<string, HistoryEntry>(new Dictionary<string, HistoryEntry>()
        {
            {"LAKSDJFLK23LKJF23LKJ23R", new HistoryEntry("LAKSDJFLK23LKJF23LKJ23R","Test song 1", "whoever1", 0) },
            {"ASDFALKSDJFLKAJSDFLKJAS", new HistoryEntry("ASDFALKSDJFLKAJSDFLKJAS","Test song 2", "whoever2", 0) },
            {"AVCIJASLDKVJAVLSKDJLKAJ", new HistoryEntry("AVCIJASLDKVJAVLSKDJLKAJ","Test song 3", "whoever3", 0) },
            {"ASDLVKJASVLDKJALKSDJFLK", new HistoryEntry("ASDLVKJASVLDKJALKSDJFLK","Test song 4", "whoever4", 0) },
            {"QWEORIUQWEORIUQOWIEURAO", new HistoryEntry("QWEORIUQWEORIUQOWIEURAO","Test song 5", "whoever5", 0) },
            {"ZXCVPOZIXCVPOIZXCVPOVIV", new HistoryEntry("ZXCVPOZIXCVPOIZXCVPOVIV","Test song 6", "whoever6", 0) },
            {"QLQFWHJLNKFLKNMWLQKCNML", new HistoryEntry("QLQFWHJLNKFLKNMWLQKCNML","Test song 7", "whoever7", 0) },
            {"TBRNEMNTMRBEBNMTEERVCVB", new HistoryEntry("TBRNEMNTMRBEBNMTEERVCVB","Test song 8", "whoever8", 0) }
        });

        public static ReadOnlyDictionary<string, HistoryEntry> TestCollection2 = new ReadOnlyDictionary<string, HistoryEntry>(new Dictionary<string, HistoryEntry>()
        {
            {"QWEMNRBQENMQBWERNBQWXCV", new HistoryEntry("QWEMNRBQENMQBWERNBQWXCV","Test song 9", "whoever1", 0) },
            {"ZXCVOIUZXCOVIUZXCVUIOZZ", new HistoryEntry("ZXCVOIUZXCOVIUZXCVUIOZZ","Test song 10", "whoever2", 0) },
            {"YXXCVBYIUXCVBIUYXCVBIUY", new HistoryEntry("YXXCVBYIUXCVBIUYXCVBIUY","Test song 11", "whoever3", 0) },
            {"MNBWMENRTBMQNWEBTMNQBWE", new HistoryEntry("MNBWMENRTBMQNWEBTMNQBWE","Test song 12", "whoever4", 0) }
        });
    }
}
