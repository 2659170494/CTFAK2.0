﻿using CTFAK.CCN;
using CTFAK.EXE;
using CTFAK.Memory;
using CTFAK.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;


namespace CTFAK.FileReaders
{
    public class ExeFileReader : IFileReader
    {
        public string Name => "Normal EXE";

        public GameData Game;
        public Dictionary<int, Bitmap> Icons = new Dictionary<int, Bitmap>();
        
        public void LoadGame(string gamePath)
        {
            Core.currentReader = this;
            Settings.gameType = Settings.GameType.NORMAL;
            var icoExt = new IconExtractor(gamePath);
            var icos = icoExt.GetAllIcons();
            foreach (var icon in icos)
            {
                Icons.Add(icon.Width, icon.ToBitmap());
            }

            if (!Icons.ContainsKey(16)) Icons.Add(16, Icons[32].resizeImage(new Size(16, 16)));
            if (!Icons.ContainsKey(48)) Icons.Add(48, Icons[32].resizeImage(new Size(48, 48)));
            if (!Icons.ContainsKey(128)) Icons.Add(128, Icons[32].resizeImage(new Size(128, 128)));
            if (!Icons.ContainsKey(256)) Icons.Add(256, Icons[32].resizeImage(new Size(256, 256)));

            var reader = new ByteReader(gamePath, System.IO.FileMode.Open);
            ReadHeader(reader);
            PackData packData=null;
            if (Settings.Old)
            {
                Settings.Unicode = false;
                if (reader.PeekInt32() != 1162690896)//PAME magic
                {
                    while (true)
                    {
                        if (reader.Tell() >= reader.Size()) break;
                        var id = reader.ReadInt16();
                        var flag = reader.ReadInt16();
                        var size = reader.ReadInt32();
                        reader.ReadBytes(size);
                        //var newChunk = new Chunk(reader);
                        //var chunkData = newChunk.Read();
                        if (id == 32639) break;
                    } 
                }
            }
            else
            {
                packData = new PackData();
                packData.Read(reader);
            }
            
            Game = new GameData();
            Game.Read(reader);
            if(!Settings.Old)Game.PackData = packData;
        }

        public int ReadHeader(ByteReader reader)
        {
            var entryPoint = CalculateEntryPoint(reader);
            reader.Seek(0);
            byte[] exeHeader = reader.ReadBytes(entryPoint);

            var firstShort = reader.PeekUInt16();

            if (firstShort == 0x7777) Settings.gameType = Settings.GameType.NORMAL;
            else/* if (firstShort == 0x222c)*/ Settings.gameType = Settings.GameType.MMF15;
            if(Settings.Old)Logger.Log($"1.5 game detected. First short: {firstShort.ToString("X")}");
            return (int)reader.Tell();
        }
        public int CalculateEntryPoint(ByteReader exeReader)
        {
            var sig = exeReader.ReadAscii(2);
            if (sig != "MZ") Logger.Log("Invalid executable signature", true, ConsoleColor.Red);

            exeReader.Seek(60);

            var hdrOffset = exeReader.ReadUInt16();

            exeReader.Seek(hdrOffset);
            var peHdr = exeReader.ReadAscii(2);
            exeReader.Skip(4);

            var numOfSections = exeReader.ReadUInt16();

            exeReader.Skip(16);
            var optionalHeader = 28 + 68;
            var dataDir = 16 * 8;
            exeReader.Skip(optionalHeader + dataDir);

            var possition = 0;
            for (var i = 0; i < numOfSections; i++)
            {
                var entry = exeReader.Tell();
                var sectionName = exeReader.ReadAscii();

                if (sectionName == ".extra")
                {
                    exeReader.Seek(entry + 20);
                    possition = (int)exeReader.ReadUInt32(); //Pointer to raw data
                    break;
                }

                if (i >= numOfSections - 1)
                {
                    exeReader.Seek(entry + 16);
                    var size = exeReader.ReadUInt32();
                    var address = exeReader.ReadUInt32(); //Pointer to raw data

                    possition = (int)(address + size);
                    break;
                }
                exeReader.Seek(entry + 40);
            }
            exeReader.Seek(possition);
            return (int)exeReader.Tell();
        }

        public GameData getGameData()
        {
            return Game;
        }

        public Dictionary<int,Bitmap> getIcons()
        {
            return Icons;
        }

        public void PatchMethods()
        {

        }
    }
}

