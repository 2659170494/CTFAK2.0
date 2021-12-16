﻿using CTFAK.CCN;
using CTFAK.CCN.Chunks;
using CTFAK.CCN.Chunks.Frame;
using CTFAK.CCN.Chunks.Objects;
using CTFAK.FileReaders;
using CTFAK.Memory;
using CTFAK.MFA;
using CTFAK.MFA.MFAObjectLoaders;
using CTFAK.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Action = CTFAK.CCN.Chunks.Frame.Action;

namespace CTFAK.Tools
{
    class FTDecompile : IFusionTool
    {
        public string Name => "Decompiler";

        public static Dictionary<int, MFAObjectInfo> FrameItems;
        public void Execute(IFileReader reader)
        {
            var game = reader.getGameData();
            var mfa = new MFAData();
            mfa.Read(new ByteReader("template.mfa", FileMode.Open));

            mfa.Name = game.name;
            mfa.LangId = 0;//8192;
            mfa.Description = "";
            mfa.Path = game.editorFilename;

            //if (game.Fonts != null) mfa.Fonts = game.Fonts;
            // mfa.Sounds.Items.Clear();
            if (game.Sounds != null && game.Sounds.Items != null)
            {

                foreach (var item in game.Sounds.Items)
                {
                    mfa.Sounds.Items.Add(item);
                }
            }
            mfa.Fonts.Items.Clear();
            if (game.Fonts?.Items != null)
            {
                foreach (var item in game.Fonts.Items)
                {
                    item.Compressed = false;
                    mfa.Fonts.Items.Add(item);
                }
            }

            mfa.Music = game.Music;
            mfa.Images.Items = game.images.Items;
            foreach (var key in mfa.Images.Items.Keys)
            {
                mfa.Images.Items[key].IsMFA = true;
            }

            // game.Images.Images.Clea r();

            mfa.Author = game.author;
            mfa.Copyright = game.copyright;
            mfa.Company = "";
            mfa.Version = "";
            //TODO:Binary Files
            var displaySettings = mfa.DisplayFlags;
            var graphicSettings = mfa.GraphicFlags;
            var flags = game.header.Flags;
            var newFlags = game.header.NewFlags;
            mfa.Extensions.Clear();

            displaySettings["MaximizedOnBoot"] = flags["Maximize"];
            displaySettings["ResizeDisplay"] = flags["MDI"];
            displaySettings["FullscreenAtStart"] = flags["FullscreenAtStart"];
            displaySettings["AllowFullscreen"] = flags["FullscreenSwitch"];
            // displaySettings["Heading"] = !flags["NoHeading"];
            // displaySettings["HeadingWhenMaximized"] = true;
            displaySettings["MenuBar"] = flags["MenuBar"];
            displaySettings["MenuOnBoot"] = !flags["MenuHidden"];
            displaySettings["NoMinimize"] = newFlags["NoMinimizeBox"];
            displaySettings["NoMaximize"] = newFlags["NoMaximizeBox"];
            displaySettings["NoThickFrame"] = newFlags["NoThickFrame"];
            // displaySettings["NoCenter"] = flags["MDI"];
            displaySettings["DisableClose"] = newFlags["DisableClose"];
            displaySettings["HiddenAtStart"] = newFlags["HiddenAtStart"];
            displaySettings["MDI"] = newFlags["MDI"];


            //mfa.GraphicFlags = graphicSettings;
            //mfa.DisplayFlags = displaySettings;
            mfa.WindowX = game.header.WindowWidth;
            mfa.WindowY = game.header.WindowHeight;
            mfa.BorderColor = game.header.BorderColor;
            mfa.HelpFile = "";
            mfa.InitialScore = game.header.InitialScore;
            mfa.InitialLifes = game.header.InitialLives;
            mfa.FrameRate = game.header.FrameRate;
            mfa.BuildType = 0;
            mfa.BuildPath = game.targetFilename;
            mfa.CommandLine = "";
            mfa.Aboutbox = game.aboutText ?? "Decompiled with CTFAK 2.0";
            //TODO: Controls

            //Object Section
            FrameItems = new Dictionary<int, MFAObjectInfo>();
            for (int i = 0; i < game.frameitems.Keys.Count; i++)
            {
                var key = game.frameitems.Keys.ToArray()[i];
                var item = game.frameitems[key];
                var newItem = TranslateObject(game,item);
                if (newItem.Loader == null)
                {
                    throw new NotImplementedException("Unsupported Object: "+newItem.ObjectType);
                }
                FrameItems.Add(newItem.Handle, newItem);
            }


            // var reference = mfa.Frames.FirstOrDefault();
            mfa.Frames.Clear();

            Dictionary<int, int> indexHandles = new Dictionary<int, int>();
            if(game.frameHandles!=null)
            {
                foreach (var pair in game.frameHandles.Items)
                {
                    var key = pair.Key;
                    var handle = pair.Value;
                    if (!indexHandles.ContainsKey(handle)) indexHandles.Add(handle, key);
                    else indexHandles[handle] = key;
                }
            }


            Logger.Log($"Prepating to translate {game.frames.Count} frames");
            for (int a = 0; a < game.frames.Count; a++)
            {
                var frame = game.frames[a];
                // if(frame.Palette==null|| frame.Events==null|| frame.Objects==null) continue;
                var newFrame = new MFAFrame(null);
                newFrame.Chunks = new MFAChunkList(null);//MFA.MFA.emptyFrameChunks;
                newFrame.Handle = a;
                if (!indexHandles.TryGetValue(a, out newFrame.Handle)) Logger.Log("Error while getting frame handle");

                newFrame.Name = frame.name;
                newFrame.SizeX = frame.width;
                newFrame.SizeY = frame.height;

                newFrame.Background = frame.background;
                newFrame.FadeIn = frame.fadeIn != null ? ConvertTransition(frame.fadeIn) : null;
                newFrame.FadeOut = frame.fadeOut != null ? ConvertTransition(frame.fadeOut) : null;
                var mfaFlags = newFrame.Flags;
                var originalFlags = frame.flags;

                mfaFlags["GrabDesktop"] = originalFlags["GrabDesktop"];
                mfaFlags["KeepDisplay"] = originalFlags["KeepDisplay"];
                mfaFlags["BackgroundCollisions"] = originalFlags["TotalCollisionMask"];
                mfaFlags["ResizeToScreen"] = originalFlags["ResizeAtStart"];
                mfaFlags["ForceLoadOnCall"] = originalFlags["ForceLoadOnCall"];
                mfaFlags["NoDisplaySurface"] = false;
                mfaFlags["TimerBasedMovements"] = originalFlags["TimedMovements"];
                newFrame.Flags = mfaFlags;
                newFrame.MaxObjects = frame.events?.MaxObjects ?? 10000;
                newFrame.Password = "";
                newFrame.LastViewedX = 320;
                newFrame.LastViewedY = 240;

                newFrame.Palette = frame.palette ?? new List<Color>();
                newFrame.StampHandle = 13;
                newFrame.ActiveLayer = 0;
                //LayerInfo
                // if(frame.Layers==null) continue;
                if (true)//Settings.GameType != GameType.OnePointFive && frame.Layers != null)
                {
                    var count = frame.layers.Items.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var layer = frame.layers.Items[i];
                        var newLayer = new MFALayer(null);
                        newLayer.Name = layer.Name;
                        newLayer.Flags["HideAtStart"] = layer.Flags["ToHide"];
                        newLayer.Flags["Visible"] = true;
                        newLayer.Flags["NoBackground"] = layer.Flags["DoNotSaveBackground"];
                        newLayer.Flags["WrapHorizontally"] = layer.Flags["WrapHorizontally"];
                        newLayer.XCoefficient = layer.XCoeff;
                        newLayer.YCoefficient = layer.YCoeff;

                        newFrame.Layers.Add(newLayer);
                    }
                }
                else
                {
                    var tempLayer = new MFALayer(null);

                    tempLayer.Name = "Layer 1";
                    tempLayer.XCoefficient = 1;
                    tempLayer.YCoefficient = 1;
                    tempLayer.Flags["Visible"] = true;
                    newFrame.Layers.Add(tempLayer);
                }


                var newFrameItems = new List<MFAObjectInfo>();
                var newInstances = new List<MFAObjectInstance>();
                if (frame.objects != null)
                // if (false)
                {

                    for (int i = 0; i < frame.objects.Count; i++)
                    {
                        var instance = frame.objects[i];
                        MFAObjectInfo frameItem;

                        if (FrameItems.ContainsKey(instance.objectInfo))
                        {
                            frameItem = FrameItems[instance.objectInfo];
                            if (!newFrameItems.Contains(frameItem)) newFrameItems.Add(frameItem);
                            var newInstance = new MFAObjectInstance((ByteReader)null);
                            newInstance.X = instance.x;
                            newInstance.Y = instance.y;
                            newInstance.Handle = instance.handle;
                            // newInstance.Flags = ((instance.FrameItem.Properties.Loader as ObjectCommon)?.Preferences?.flag ?? (uint)instance.FrameItem.Flags);
                            newInstance.Flags = (uint)instance.flags;

                            newInstance.ParentType = (uint)instance.parentType;
                            newInstance.ItemHandle = (uint)(instance.objectInfo);
                            newInstance.ParentHandle = (uint)instance.parentHandle;
                            newInstance.Layer = (uint)(instance.layer);
                            newInstances.Add(newInstance);
                        }
                        else
                        {
                            Logger.Log("WARNING: OBJECT NOT FOUND");
                            break;
                        }
                    }
                }


                newFrame.Items = newFrameItems;
                newFrame.Instances = newInstances;
                newFrame.Folders = new List<MFAItemFolder>();
                foreach (MFAObjectInfo newFrameItem in newFrame.Items)
                {
                    var newFolder = new MFAItemFolder((ByteReader)null);
                    newFolder.isRetard = true;
                    newFolder.Items = new List<uint>() { (uint)newFrameItem.Handle };
                    newFrame.Folders.Add(newFolder);
                }
                // if(false)
                {
                    newFrame.Events = new MFAEvents((ByteReader)null);
                    newFrame.Events.Items = new List<EventGroup>();
                    newFrame.Events.Objects = new List<EventObject>();
                    newFrame.Events._ifMFA = true;
                    newFrame.Events.Version = 1028;
                    // if(false)
                    if (frame.events != null)
                    {
                        foreach (var item in newFrame.Items)
                        {
                            var newObject = new EventObject((ByteReader)null);

                            newObject.Handle = (uint)item.Handle;
                            newObject.Name = item.Name ?? "";
                            newObject.TypeName = "";
                            newObject.ItemType = (ushort)item.ObjectType;
                            newObject.ObjectType = 1;
                            newObject.Flags = 0;
                            newObject.ItemHandle = (uint)item.Handle;
                            newObject.InstanceHandle = 0xFFFFFFFF;
                            newFrame.Events.Objects.Add(newObject);
                        }

                        newFrame.Events.Items = frame.events.Items;
                        Dictionary<int, Quailifer> qualifiers = new Dictionary<int, Quailifer>();
                        foreach (Quailifer quailifer in frame.events.QualifiersList.Values)
                        {
                            break;
                            int newHandle = 0;
                            while (true)
                            {
                                if (!newFrame.Items.Any(item => item.Handle == newHandle) &&
                                    !qualifiers.Keys.Any(item => item == newHandle)) break;
                                newHandle++;

                            }
                            qualifiers.Add(newHandle, quailifer);
                            var qualItem = new EventObject(null as ByteReader);
                            qualItem.Handle = (uint)newHandle;
                            qualItem.SystemQualifier = (ushort)quailifer.Qualifier;
                            qualItem.Name = "";
                            qualItem.TypeName = "";
                            qualItem.ItemType = (ushort)quailifer.Type;
                            qualItem.ObjectType = 3;
                            newFrame.Events.Objects.Add(qualItem);

                        }
                        foreach (EventGroup eventGroup in newFrame.Events.Items)
                        {
                            foreach (Action action in eventGroup.Actions)
                            {
                                foreach (var quailifer in qualifiers)
                                {
                                    if (quailifer.Value.ObjectInfo == action.ObjectInfo)
                                        action.ObjectInfo = quailifer.Key;
                                    foreach (var param in action.Items)
                                    {
                                        var objInfoFld = param?.Loader?.GetType()?.GetField("ObjectInfo");
                                        if (objInfoFld == null) continue;
                                        if ((int)objInfoFld?.GetValue(param?.Loader) ==
                                            quailifer.Value?.ObjectInfo)
                                            param.Loader?.GetType().GetField("ObjectInfo")
                                                .SetValue(param.Loader, quailifer.Key);
                                    }
                                }

                            }
                            foreach (Condition cond in eventGroup.Conditions)
                            {
                                foreach (var quailifer in qualifiers)
                                {
                                    if (quailifer.Value.ObjectInfo == cond.ObjectInfo)
                                        cond.ObjectInfo = quailifer.Key;
                                    foreach (var param in cond.Items)
                                    {
                                        var objInfoFld = param?.Loader?.GetType()?.GetField("ObjectInfo");
                                        if (objInfoFld == null) continue;
                                        if ((int)objInfoFld?.GetValue(param?.Loader) ==
                                            quailifer.Value?.ObjectInfo)
                                            param.Loader?.GetType().GetField("ObjectInfo")
                                                .SetValue(param.Loader, quailifer.Key);
                                    }
                                }
                            }
                        }

                    }
                }
                mfa.Frames.Add(newFrame);
            }
            mfa.Write(new ByteWriter(new FileStream($"Dumps\\{Path.GetFileNameWithoutExtension(game.editorFilename)}.mfa", FileMode.Create)));
        }

        public static MFATransition ConvertTransition(Transition gameTrans)
        {
            var mfaTrans = new MFATransition((ByteReader)null)
            {
                Module = "cctrans.dll",//gameTrans.ModuleFile,
                Name = "Transition",
                Id = gameTrans.Module,
                TransitionId = gameTrans.Name,
                Flags = gameTrans.Flags,
                Color = gameTrans.Color,
                ParameterData = gameTrans.ParameterData,
                Duration = gameTrans.Duration
            };
            return mfaTrans;

        }



        public static MFAObjectInfo TranslateObject(GameData game, ObjectInfo item)
        {
            var newItem = new MFAObjectInfo(null);
            newItem.Chunks = new MFAChunkList(null);
            newItem.Name = item.name;
            newItem.ObjectType = (int)item.ObjectType;
            newItem.Handle = item.handle;
            newItem.Transparent = 1;
            newItem.InkEffect = item.InkEffect;
            newItem.InkEffectParameter = (uint)item.InkEffectValue;
            newItem.AntiAliasing = 0;
            newItem.Flags = item.Flags;
            
            // newItem.Chunks.GetOrCreateChunk<Opacity>().Blend = (byte) (item.InkEffectValue);
            // newItem.Chunks.GetOrCreateChunk<Opacity>().RGBCoeff = Color.White;

            newItem.IconHandle = 12;



            if (item.ObjectType == (int)Constants.ObjectType.QuickBackdrop)
            {
                /*var backdropLoader = item.properties as Backdrop;
                var backdrop = new MFAQuickBackdrop((ByteReader)null);
                backdrop.ObstacleType = (uint)backdropLoader.ObstacleType;
                backdrop.CollisionType = (uint)backdropLoader.CollisionType;
                backdrop.Width = backdropLoader.Width;
                backdrop.Height = backdropLoader.Height;
                backdrop.Shape = backdropLoader.Shape.ShapeType;
                backdrop.BorderSize = backdropLoader.Shape.BorderSize;
                backdrop.FillType = backdropLoader.Shape.FillType;
                backdrop.Color1 = backdropLoader.Shape.Color1;
                backdrop.Color2 = backdropLoader.Shape.Color2;
                backdrop.Flags = backdropLoader.Shape.GradFlags;
                backdrop.Image = backdropLoader.Shape.Image;
                newItem.Loader = backdrop;*/
                throw new NotImplementedException("QUICKBACKDROP IS NOT IMPLEMENTED");

            }
            else if (item.ObjectType == (int)Constants.ObjectType.Backdrop)
            {
                var backdropLoader = item.properties as Backdrop;
                var backdrop = new MFABackdrop((ByteReader)null);
                backdrop.ObstacleType = (uint)backdropLoader.ObstacleType;
                backdrop.CollisionType = (uint)backdropLoader.CollisionType;
                backdrop.Handle = backdropLoader.Image;
                newItem.Loader = backdrop;
            }
            else
            {
                var itemLoader = item?.properties as ObjectCommon;
                if (itemLoader == null) throw new NotImplementedException("Null loader");
                //CommonSection
                var newObject = new ObjectLoader(null);
                newObject.ObjectFlags = (int)(itemLoader.Flags.flag);
                newObject.NewObjectFlags = (int)(itemLoader.NewFlags.flag);
                newObject.BackgroundColor = itemLoader.BackColor;
                newObject.Qualifiers = itemLoader._qualifiers;

                newObject.Strings = new MFAValueList(null);//ConvertStrings(itemLoader.);
                newObject.Values = new MFAValueList(null);//ConvertValue(itemLoader.Values);
                newObject.Movements = new MFAMovements(null);
                if (itemLoader.Movements == null)
                {
                    var newMov = new MFAMovement(null);
                    newMov.Name = $"Movement #{0}";
                    newMov.Extension = "";
                    newMov.Type = 0;
                    newMov.Identifier = (uint)0;
                    newMov.Loader = null;
                    newMov.Player = 0;
                    newMov.MovingAtStart = 1;
                    newMov.DirectionAtStart = 0;
                    newObject.Movements.Items.Add(newMov);

                }
                else
                {
                    for (int j = 0; j < itemLoader.Movements.Items.Count; j++)
                    {
                        var mov = itemLoader.Movements.Items[j];
                        var newMov = new MFAMovement(null);
                        newMov.Name = $"Movement #{j}";
                        newMov.Extension = "";
                        newMov.Type = mov.Type;
                        newMov.Identifier = (uint)mov.Type;
                        newMov.Loader = mov.Loader;
                        newMov.Player = mov.Player;
                        newMov.MovingAtStart = mov.MovingAtStart;
                        newMov.DirectionAtStart = mov.DirectionAtStart;
                        newObject.Movements.Items.Add(newMov);
                    }
                }


                newObject.Behaviours = new Behaviours(null);

                if (item.ObjectType == (int)Constants.ObjectType.Active)
                {
                    var active = new MFAActive(null);
                    //Shit Section
                    {
                        active.ObjectFlags = newObject.ObjectFlags;
                        active.NewObjectFlags = newObject.NewObjectFlags;
                        active.BackgroundColor = newObject.BackgroundColor;
                        active.Strings = newObject.Strings;
                        active.Values = newObject.Values;
                        active.Movements = newObject.Movements;
                        active.Behaviours = newObject.Behaviours;
                        active.Qualifiers = newObject.Qualifiers;
                    }


                    //TODO: Transitions
                    if (itemLoader.Animations != null)
                    {
                        var animHeader = itemLoader.Animations;
                        for (int j = 0; j < animHeader.AnimationDict.Count; j++)
                        {
                            var origAnim = animHeader.AnimationDict.ToArray()[j];
                            var newAnimation = new MFAAnimation(null);
                            newAnimation.Name = $"User Defined {j}";
                            var newDirections = new List<MFAAnimationDirection>();
                            Animation animation = null;
                            if (animHeader.AnimationDict.ContainsKey(origAnim.Key))
                            {
                                animation = animHeader?.AnimationDict[origAnim.Key];
                            }
                            else break;

                            if (animation != null)
                            {
                                if (animation.DirectionDict != null)
                                {
                                    for (int n = 0; n < animation.DirectionDict.Count; n++)
                                    {
                                        var direction = animation.DirectionDict.ToArray()[n].Value;
                                        var newDirection = new MFAAnimationDirection(null);
                                        newDirection.MinSpeed = direction.MinSpeed;
                                        newDirection.MaxSpeed = direction.MaxSpeed;
                                        newDirection.Index = n;
                                        newDirection.Repeat = direction.Repeat;
                                        newDirection.BackTo = direction.BackTo;
                                        newDirection.Frames = direction.Frames;
                                        newDirections.Add(newDirection);
                                    }
                                }
                                else
                                {

                                }

                                newAnimation.Directions = newDirections;
                            }

                            active.Items.Add(j, newAnimation);
                        }
                    }

                    newItem.Loader = active;
                }

                if ((int)item.ObjectType >= 32)
                {
                    var newExt = new MFAExtensionObject(null);
                    {
                        newExt.ObjectFlags = newObject.ObjectFlags;
                        newExt.NewObjectFlags = newObject.NewObjectFlags;
                        newExt.BackgroundColor = newObject.BackgroundColor;
                        newExt.Strings = newObject.Strings;
                        newExt.Values = newObject.Values;
                        newExt.Movements = newObject.Movements;
                        newExt.Behaviours = newObject.Behaviours;
                        newExt.Qualifiers = newObject.Qualifiers;

                    }
                    // if (Settings.GameType != GameType.OnePointFive)
                    {
                        Extensions exts = game.extensions;
                        Extension ext = null;
                        foreach (var testExt in exts.Items)
                        {
                            if (testExt.Handle == (int)item.ObjectType - 32) ext = testExt;
                        }

                        newExt.ExtensionType = -1;
                        newExt.ExtensionName = "";
                        newExt.Filename = $"{ext.Name}.mfx";
                        newExt.Magic = (uint)ext.MagicNumber;
                        newExt.SubType = ext.SubType;
                        newExt.ExtensionVersion = itemLoader.ExtensionVersion;
                        newExt.ExtensionId = itemLoader.ExtensionId;
                        newExt.ExtensionPrivate = itemLoader.ExtensionPrivate;
                        newExt.ExtensionData = itemLoader.ExtensionData;
                        newItem.Loader = newExt;
                        var tuple = new Tuple<int, string, string, int, string>(ext.Handle, ext.Name, "",
                            ext.MagicNumber, ext.SubType);
                        // mfa.Extensions.Add(tuple);
                    }

                }
                else if (item.ObjectType == (int)Constants.ObjectType.Text)
                {
                    var text = itemLoader.Text;
                    var newText = new MFAText(null);
                    //Shit Section
                    {
                        newText.ObjectFlags = newObject.ObjectFlags;
                        newText.NewObjectFlags = newObject.NewObjectFlags;
                        newText.BackgroundColor = newObject.BackgroundColor;
                        newText.Strings = newObject.Strings;
                        newText.Values = newObject.Values;
                        newText.Movements = newObject.Movements;
                        newText.Behaviours = newObject.Behaviours;
                        newText.Qualifiers = newObject.Qualifiers;

                    }
                    if (text == null)
                    {
                        newText.Width = 10;
                        newText.Height = 10;
                        newText.Font = 0;
                        newText.Color = Color.Black;
                        newText.Flags = 0;
                        newText.Items = new List<MFAParagraph>(){new MFAParagraph(null)
                        {
                            Value="ERROR"
                        }};
                    }
                    else
                    {
                        newText.Width = (uint)text.Width;
                        newText.Height = (uint)text.Height;
                        var paragraph = text.Items[0];
                        newText.Font = paragraph.FontHandle;
                        newText.Color = paragraph.Color;
                        newText.Flags = paragraph.Flags.flag;
                        newText.Items = new List<MFAParagraph>();
                        foreach (Paragraph exePar in text.Items)
                        {
                            var newPar = new MFAParagraph((ByteReader)null);
                            newPar.Value = exePar.Value;
                            newPar.Flags = exePar.Flags.flag;
                            newText.Items.Add(newPar);
                        }
                    }


                    newItem.Loader = newText;

                }
                else if (item.ObjectType == (int)Constants.ObjectType.Lives || item.ObjectType == (int)Constants.ObjectType.Score)
                {
                    /*var counter = itemLoader.Counters;
                    var lives = new Counter(null);
                    {
                        lives.ObjectFlags = newObject.ObjectFlags;
                        lives.NewObjectFlags = newObject.NewObjectFlags;
                        lives.BackgroundColor = newObject.BackgroundColor;
                        lives.Strings = newObject.Strings;
                        lives.Values = newObject.Values;
                        lives.Movements = newObject.Movements;
                        lives.Behaviours = newObject.Behaviours;
                        lives.Qualifiers = newObject.Qualifiers;

                    }
                    lives.Player = counter?.Player ?? 0;
                    lives.Images = counter?.Frames ?? new List<int>() { 0 };
                    lives.DisplayType = counter?.DisplayType ?? 0;
                    lives.Flags = counter?.Flags ?? 0;
                    lives.Font = counter?.Font ?? 0;
                    lives.Width = (int)(counter?.Width ?? 0);
                    lives.Height = (int)(counter?.Height ?? 0);
                    newItem.Loader = lives;*/
                    throw new NotImplementedException("LIVES OBJECT IS NOT IMPLEMENTED");

                }
                else if (item.ObjectType == (int)Constants.ObjectType.Counter)
                {
                    var counter = itemLoader.Counters;
                    var newCount = new MFACounter(null);
                    {
                        newCount.ObjectFlags = newObject.ObjectFlags;
                        newCount.NewObjectFlags = newObject.NewObjectFlags;
                        newCount.BackgroundColor = newObject.BackgroundColor;
                        newCount.Strings = newObject.Strings;
                        newCount.Values = newObject.Values;
                        newCount.Movements = newObject.Movements;
                        newCount.Behaviours = newObject.Behaviours;
                        newCount.Qualifiers = newObject.Qualifiers;

                    }
                    if (itemLoader.Counter == null)
                    {
                        newCount.Value = 0;
                        newCount.Minimum = 0;
                        newCount.Maximum = 0;

                    }
                    else
                    {
                        newCount.Value = itemLoader.Counter.Initial;
                        newCount.Maximum = itemLoader.Counter.Maximum;
                        newCount.Minimum = itemLoader.Counter.Minimum;
                    }

                    var shape = counter?.Shape;
                    // if(counter==null) throw new NullReferenceException(nameof(counter));
                    // counter = null;
                    // shape = null;
                    if (counter == null)
                    {
                        newCount.DisplayType = 0;
                        newCount.CountType = 0;
                        newCount.Width = 0;
                        newCount.Height = 0;
                        newCount.Images = new List<int>() { 0 };
                        newCount.Font = 0;
                    }
                    else
                    {
                        newCount.DisplayType = counter.DisplayType;
                        newCount.CountType = counter.Inverse ? 1 : 0;
                        newCount.Width = (int)counter.Width;
                        newCount.Height = (int)counter.Height;
                        newCount.Images = counter.Frames;
                        newCount.Font = counter.Font;
                    }

                    if (shape == null)
                    {
                        newCount.Color1 = Color.Black;
                        newCount.Color2 = Color.Black;
                        newCount.VerticalGradient = 0;
                        newCount.CountFlags = 0;
                    }
                    else
                    {
                        newCount.Color1 = shape.Color1;
                        newCount.Color2 = shape.Color2;
                        newCount.VerticalGradient = (uint)shape.GradFlags;
                        newCount.CountFlags = (uint)shape.FillType;

                    }

                    newItem.Loader = newCount;
                }

            }

            return newItem;
        }
    }
}