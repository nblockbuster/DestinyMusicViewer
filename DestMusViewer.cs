using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WwiseParserLib;
using WwiseParserLib.Structures.Chunks;
using WwiseParserLib.Structures.Objects.HIRC;
using WwiseParserLib.Structures.Objects.HIRC.Structs;
using WwiseParserLib.Structures.SoundBanks;

namespace DestinyMusicViewer
{
    public class DestMusViewer
    {
        public SoundBank MemorySoundbank { get; set; }
        public List<string> genList(byte[] soundBankData, ref ConcurrentDictionary<string, List<uint>> id_to_segment)
        {
            ConcurrentDictionary<string, List<uint>> id_segment = new ConcurrentDictionary<string, List<uint>>();
            id_segment = id_to_segment;
            List<string> GinsorIDs = new List<string>();
            SoundBank memSoundBank = new InMemorySoundBank(soundBankData);
            var bkhd = memSoundBank.ParseChunk(SoundBankChunkType.BKHD);
            if (bkhd == null)
            {
                throw new Exception("The specified file does not have a valid SoundBank header.");
            }
            var hirc = memSoundBank.GetChunk(SoundBankChunkType.HIRC);
            if (hirc == null)
            {
                throw new Exception("The specified file does not have a valid Hierarchy header.");
            }

            var musicObjs = (hirc as SoundBankHierarchyChunk).Objects.Where(o => o is MusicObject).Select(o => o as MusicObject);

            Parallel.ForEach(musicObjs, obj =>
            {
                if (obj.Type == HIRCObjectType.MusicSegment)
                {
                    var segment = obj as MusicSegment;
                    for (int i = 0; i < segment.ChildCount; i++)
                    {
                        foreach (var srch_obj in musicObjs)
                        {
                            if (srch_obj.Id == segment.ChildIds[i])
                            {
                                var track = srch_obj as MusicTrack;
                                for (int x = 0; x < track.SoundCount; x++)
                                {
                                    var sound = track.Sounds[x];
                                    var ginsid = ((uint)IPAddress.NetworkToHostOrder((int)sound.AudioId)).ToHex().ToUpper();
                                    Debug.WriteLine($"GinsorID of track {track.Id} (Parent Segment: {segment.Id}): {ginsid}");
                                    if (!id_segment.ContainsKey(ginsid) || id_segment[ginsid] == null)
                                    {
                                        id_segment[ginsid] = new List<uint>();
                                    }
                                    id_segment[ginsid].Add(segment.Id);
                                    GinsorIDs.Add(ginsid);
                                }
                            }
                        }
                    }
                }
            });
            return GinsorIDs;
        }

        public List<string> GenHierList(MusicPlaylistElement list)
        {
            List<string> SerializedStrings = new List<string>();
            string spaces = "----";
            if (list.ChildCount != 0)
            {
                var String1 = $"|{spaces}{list.Type} [{NetworkToHost(list.UnknownId)}]";
                SerializedStrings.Add(String1);
                Recursive(list.Children, ref SerializedStrings);
            }
            return SerializedStrings;
        }

        int it = 8;
        MusicPlaylistElement ThePreviousOne = null;
        MusicPlaylistElement Playlist = null;
        public void Recursive(MusicPlaylistElement[] list, ref List<string> SerializedStrings)
        {
            string spaces = new string('-', it);
            foreach (var ListChild in list)
            {
                //it += 4;
                if (ListChild.IsGroup)
                {
                    if (Playlist.Children[0].Children.Contains(ListChild))
                        it += 4;
                    else if (it != 8)
                        it += 4;
                    spaces = new string('-', it);
                    var String2 = $"|{spaces}{ListChild.Type} [{NetworkToHost(ListChild.UnknownId)}] | Shuffle: {ListChild.IsShuffle} | Loop Count: {ListChild.LoopCount}";
                    SerializedStrings.Add(String2);
                    ThePreviousOne = ListChild;
                    Recursive(ListChild.Children, ref SerializedStrings);
                }
                else if (ListChild.Type == MusicPlaylistElementType.MusicSegment)
                {
                    foreach (var searching_obj in (MemorySoundbank.GetChunk(SoundBankChunkType.HIRC) as SoundBankHierarchyChunk).Objects.Where(o => o is MusicSegment).Select(o => o as MusicSegment))
                    {
                        if (searching_obj.Id == ListChild.SegmentId)
                        {
                            spaces = new string(' ', it+8);
                            string SegmentString = $"|{spaces}MusicSegment [{NetworkToHost(searching_obj.Id)}] | Tempo: {searching_obj.Tempo} | Time Signature: {searching_obj.TimeSignatureUpper}/{searching_obj.TimeSignatureLower}";
                            if (searching_obj.Properties.ParameterCount != 0 && searching_obj.Properties.ParameterTypes[0] == AudioParameterType.VoiceVolume)
                            {
                                SegmentString += $" | Volume: {searching_obj.Properties.ParameterValues[0]}";
                            }
                            SerializedStrings.Add(SegmentString);
                            foreach (var childid in searching_obj.ChildIds)
                            {
                                foreach (var track_obj in (MemorySoundbank.GetChunk(SoundBankChunkType.HIRC) as SoundBankHierarchyChunk).Objects.Where(o => o is MusicTrack).Select(o => o as MusicTrack))
                                {
                                    if (track_obj.Id == childid)
                                    {
                                        spaces = new string(' ', it+12);
                                        string TrackString = $"|{spaces}MusicTrack [{NetworkToHost(track_obj.Id)}]";
                                        if (track_obj.Properties.ParameterCount != 0 && track_obj.Properties.ParameterTypes[0] == AudioParameterType.VoiceVolume)
                                        {
                                            TrackString += $" | Volume: {track_obj.Properties.ParameterValues[0]}";
                                        }

                                        if (track_obj.SoundCount == 0)
                                        {
                                            continue;
                                        }

                                        string GinsorId = NetworkToHost(track_obj.Sounds[0].AudioId);
                                        spaces = new string(' ', it+16);
                                        string GinsorIdString = $"|{spaces}Src GinsorID: {GinsorId}";
                                        SerializedStrings.Add(TrackString);
                                        SerializedStrings.Add(GinsorIdString);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        public string NetworkToHost(uint input)
        {
            return ((uint)IPAddress.NetworkToHostOrder((int)input)).ToHex().ToUpper();
        }

        public Dictionary<string, List<string>> ParseBnkForPlaylists(SoundBank in_mem_soundbank)
        {
            MemorySoundbank = in_mem_soundbank;
            Dictionary<string, List<string>> SerializedStringsDict = new Dictionary<string, List<string>>();
            List<string> SerializedStrings = new List<string>();
            List<uint> PlaylistContainerIds = new List<uint>();
            var bkhd = MemorySoundbank.ParseChunk(SoundBankChunkType.BKHD);
            if (bkhd == null)
            {
                throw new Exception("The specified file does not have a valid SoundBank header.");
            }
            var hirc = MemorySoundbank.GetChunk(SoundBankChunkType.HIRC);
            if (hirc == null)
            {
                throw new Exception("The specified file does not have a valid Hierarchy header.");
            }
            var musicObjs = (hirc as SoundBankHierarchyChunk).Objects.Where(o => o is MusicObject).Select(o => o as MusicObject);
            foreach (var obj in musicObjs)
            {
                if (obj.Type != HIRCObjectType.MusicSwitchContainer || obj.Properties.ParentId == 0)
                    continue;
                string SwitchContainerString = $"MusicSwitchContainer [{NetworkToHost(obj.Id)}] | Tempo: {obj.Tempo.ToString("0.0")} | Time Signature: {obj.TimeSignatureUpper}/{obj.TimeSignatureLower}";
                if (obj.Properties.ParameterCount != 0 && obj.Properties.ParameterTypes[0] == AudioParameterType.VoiceVolume)
                {
                    SwitchContainerString += $" | Volume: {obj.Properties.ParameterValues[0]}";
                }
                SwitchContainerString += "\n";
                Debug.WriteLine(SwitchContainerString);
                SerializedStrings.Add(SwitchContainerString);

                foreach (var ChildId in obj.ChildIds)
                {
                    foreach (var playlist_obj in musicObjs)
                    {
                        if (playlist_obj.Id == ChildId && playlist_obj.Type == HIRCObjectType.MusicPlaylistContainer)
                            PlaylistContainerIds.Add(ChildId);
                    }
                }
                uint PlaylistTracker = 0;
                foreach (var PlaylistId in PlaylistContainerIds)
                {
                    foreach (var playlist_obj in musicObjs)
                    {
                        if (playlist_obj.Id == PlaylistId)
                        {
                            string PlaylistContainerString = $"MusicPlaylistContainer [{NetworkToHost(PlaylistId)}] #{PlaylistTracker} | Tempo: {playlist_obj.Tempo.ToString("0.0")} | Time Signature: {playlist_obj.TimeSignatureUpper}/{playlist_obj.TimeSignatureLower}";
                            if (playlist_obj.Properties.ParameterCount != 0 && playlist_obj.Properties.ParameterTypes[0] == AudioParameterType.VoiceVolume)
                            {
                                PlaylistContainerString += $" | Volume: {playlist_obj.Properties.ParameterValues[0]}";
                            }
                            Debug.WriteLine(PlaylistContainerString);
                            
                            var List = (playlist_obj as MusicPlaylistContainer).Playlist;
                            Playlist = List;
                            SerializedStringsDict[NetworkToHost(PlaylistId)] = GenHierList(List);
                            SerializedStringsDict[NetworkToHost(PlaylistId)].Insert(0, SwitchContainerString);
                            SerializedStringsDict[NetworkToHost(PlaylistId)].Insert(1, PlaylistContainerString);
                        }
                    }

                    PlaylistTracker++;
                }
            }
            return SerializedStringsDict;
        }
    }
}
