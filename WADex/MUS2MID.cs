using System.IO;
using System;

namespace musConvert
{
    public static class MUS2MID
    {
        private class musheader
        {
            public byte[] id;
            public ushort scorelength;
            public ushort scorestart;
            public ushort primarychannels;
            public ushort secondarychannels;
            public ushort instrumentcount;

            public musheader()
            {
                id = new byte[4];
                scorelength = scorestart = primarychannels = secondarychannels = instrumentcount = 0;
            }
        }

        private enum musevent : byte
        {
            mus_releasekey = 0x00,
            mus_presskey = 0x10,
            mus_pitchwheel = 0x20,
            mus_systemevent = 0x30,
            mus_changecontroller = 0x40,
            mus_scoreend = 0x60
        }

        private enum midievent : byte
        {
            midi_releasekey = 0x80,
            midi_presskey = 0x90,
            midi_aftertouchkey = 0xA0,
            midi_changecontroller = 0xB0,
            midi_changepatch = 0xC0,
            midi_aftertouchchannel = 0xD0,
            midi_pitchwheel = 0xE0
        }

        private const int NUM_CHANNELS = 16;
        private const int MIDI_PERCUSSION_CHAN = 9;
        private const int MUS_PERCUSSION_CHAN = 15;

        private static readonly byte[] midiheader;
        private static byte[] channelvelocities, controller_map;
        private static uint queuedtime, tracksize;
        private static int[] channel_map;

        static MUS2MID()
        {
            midiheader = new byte[]
            {
                B('M'), B('T'), B('h'), B('d'),
                0x00,   0x00,   0x00,   0x06,
                0x00,   0x00,
                0x00,   0x01,
                0x00,   0x46,
                B('M'), B('T'), B('r'), B('k'),
                0x00,   0x00,   0x00,   0x00
            };
        }

        private static byte B(char C)
        {
            return (byte)C;
        }

        private static void Init()
        {
            channelvelocities = new byte[]
            {
                127, 127, 127, 127, 127, 127, 127, 127,
                127, 127, 127, 127, 127, 127, 127, 127
            };
            controller_map = new byte[]
            {
                0x00, 0x20, 0x01, 0x07, 0x0A, 0x0B, 0x5B, 0x5D,
                0x40, 0x43, 0x78, 0x7B, 0x7E, 0x7F, 0x79
            };
            channel_map = new int[NUM_CHANNELS];
            tracksize = queuedtime = 0;
        }

        public static bool Convert(Stream In, Stream Out)
        {
            Init();
            // Header for the MUS file
            musheader Header = new musheader();
            // Descriptor for the current MUS event
            byte eventdescriptor;
            // Channel number
            byte channel;
            // Current Event
            musevent Event;

            // Bunch of vars read from MUS lump
            byte key;
            byte controllernumber = 0;
            byte controllervalue = 0;

            // Flag for when the score end marker is hit.
            bool hitscoreend = false;

            // Temp working byte
            byte working;
            // Used in building up time delays
            uint timedelay;

            for (channel = 0; channel < NUM_CHANNELS; ++channel)
            {
                channel_map[channel] = -1;
            }

            // Grab the header
            if (!ReadMusHeader(In, Header))
            {
                return true;
            }


            // Check MUS header
            if (Header.id[0] != 'M'
              || Header.id[1] != 'U'
              || Header.id[2] != 'S'
              || Header.id[3] != 0x1A)
            {
                return true;
            }

            In.Seek(Header.scorestart,SeekOrigin.Begin);

            // So, we can assume the MUS file is faintly legit. Let's start
            // writing MIDI data...

            Out.Write(midiheader, 0, midiheader.Length);

            tracksize = 0;

            // Now, process the MUS file:
            while (!hitscoreend)
            {
                // Handle a block of events:
                while (!hitscoreend)
                {
                    // Fetch channel number and event code:
                    eventdescriptor = (byte)In.ReadByte();
                    channel = GetMIDIChannel(eventdescriptor & 0x0F);
                    Event = (musevent)(eventdescriptor & 0x70);
                    switch (Event)
                    {
                        case musevent.mus_releasekey:
                            key=(byte)In.ReadByte();
                            if (WriteReleaseKey(channel, key, Out))
                            {
                                return true;
                            }
                            break;
                        case musevent.mus_presskey:
                            key=(byte)In.ReadByte();
                            if (key > 0x7F)
                            {
                                channelvelocities[channel]=(byte)In.ReadByte();
                                channelvelocities[channel] &= 0x7F;
                            }

                            if (WritePressKey(channel, key, channelvelocities[channel], Out))
                            {
                                return true;
                            }

                            break;

                        case musevent.mus_pitchwheel:
                            key = (byte)In.ReadByte();
                            if (WritePitchWheel(channel, (short)(key * 64), Out))
                            {
                                return true;
                            }
                        break;

                        case musevent.mus_systemevent:
                            controllernumber = (byte)In.ReadByte();
                            if (controllernumber < 10 || controllernumber > 14)
                            {
                                return true;
                            }

                            if (WriteChangeController_Valueless(channel,controller_map[controllernumber],Out))
                            {
                                return true;
                            }
                            break;

                        case musevent.mus_changecontroller:
                            controllernumber = (byte)In.ReadByte();
                            controllervalue = (byte)In.ReadByte();
                            if (controllernumber == 0)
                            {
                                if (WriteChangePatch(channel, controllervalue, Out))
                                {
                                    return true;
                                }
                            }
                            else
                            {
                                if (controllernumber < 1 || controllernumber > 9)
                                {
                                    return true;
                                }

                                if (WriteChangeController_Valued(channel, controller_map[controllernumber], controllervalue, Out))
                                {
                                    return true;
                                }
                            }

                        break;

                        case musevent.mus_scoreend:
                            hitscoreend = true;
                            break;
                        default:
                            return true;
                    }

                    if (eventdescriptor> 0x7F)
                    {
                        break;
                    }
                } //INNER
                // Now we need to read the time code:
                if (!hitscoreend)
                {
                    timedelay = 0;
                    while(true)
                    {
                        working = (byte)In.ReadByte();
                        timedelay = timedelay * 128 + (uint)(working & 0x7F);
                        if (working < 0x80)
                        {
                            break;
                        }
                    }
                    queuedtime += timedelay;
                }
            } //OUTER

            // End of track
            if (WriteEndTrack(Out))
            {
                return true;
            }
            Out.Seek(18, SeekOrigin.Begin);

            Out.Write(new byte[]
            { 
                (byte)((tracksize >> 24) & 0xFF),
                (byte)((tracksize >> 16) & 0xFF),
                (byte)((tracksize >> 8) & 0xFF),
                (byte)(tracksize & 0xFF)}, 0, 4);
            Out.Flush();
            return false;
        }

        private static bool WriteTime(uint time, Stream Output)
        {
            uint buffer = time & 0x7F;
            byte writeval = 0;
            while ((time >>= 7) != 0)
            {
                buffer <<= 8;
                buffer |= ((time & 0x7F) | 0x80);
            }
            while(true)
            {
                writeval = (byte)(buffer & 0xFF);

                Output.WriteByte(writeval);

                ++tracksize;

                if ((buffer & 0x80) != 0)
                {
                    buffer >>= 8;
                }
                else
                {
                    queuedtime = 0;
                    return false;
                }
            }
        }

        private static bool WriteEndTrack(Stream Output)
        {
            byte[] endtrack = {0xFF, 0x2F, 0x00};
            if (WriteTime(queuedtime, Output))
            {
                return true;
            }

            Output.Write(endtrack, 0, 3);

            tracksize += 3;
            return false;
        }

        private static bool WriteKey(midievent Event, byte channel, byte key, byte velocity, Stream Output)
        {
            if (WriteTime(queuedtime, Output))
            {
                return true;
            }
            Output.Write(
                new byte[]
                {
                    (byte)((byte)Event | channel),
                    (byte)(key & 0x7F),
                    (byte)(velocity & 0x7F)
                }, 0, 3);
            tracksize += 3;
            return false;
        }

        private static bool WritePressKey(byte channel, byte key, byte velocity, Stream Output)
        {
            return WriteKey(midievent.midi_presskey, channel, key, velocity, Output);
        }

        private static bool WriteReleaseKey(byte channel, byte key, Stream Output)
        {
            return WriteKey(midievent.midi_releasekey, channel, key, 0, Output);
        }

        private static bool WritePitchWheel(byte channel, short wheel, Stream Output)
        {
            if (WriteTime(queuedtime, Output))
            {
                return true;
            }
            Output.Write(
                new byte[]
                {
                    (byte)((byte)midievent.midi_pitchwheel | channel),
                    (byte)(wheel & 0x7F),
                    (byte)((wheel >> 7) & 0x7F)
                }, 0, 3);
            tracksize += 3;
            return false;
        }

        private static bool WriteChangePatch(byte channel, byte patch, Stream Output)
        {
            if (WriteTime(queuedtime, Output))
            {
                return true;
            }
            Output.Write(
                new byte[]
                {
                    (byte)((byte)midievent.midi_changepatch | channel),
                    (byte)(patch & 0x7F),
                }, 0, 2);
            tracksize += 2;
            return false;

        }

        private static bool WriteChangeController_Valued(byte channel, byte control, byte value, Stream Output)
        {
            if (WriteTime(queuedtime, Output))
            {
                return true;
            }
            Output.Write(
                new byte[]
                {
                    (byte)((byte)midievent.midi_changecontroller | channel),
                    (byte)(control & 0x7F),
                    value > 0x7F ? (byte)0x7F : value
                }, 0, 3);
            tracksize += 3;
            return false;
        }

        private static bool WriteChangeController_Valueless(byte channel, byte control, Stream Output)
        {
            return WriteChangeController_Valued(channel, control, 0, Output);
        }

        private static int AllocateMIDIChannel()
        {
            int max=-1;
            for (int i = 0; i < NUM_CHANNELS; ++i)
            {
                if (channel_map[i] > max)
                {
                    max = channel_map[i];
                }
            }

            // max is now equal to the highest-allocated MIDI channel.  We can
            // now allocate the next available channel.  This also works if
            // no channels are currently allocated (max=-1)

            // Don't allocate the MIDI percussion channel!
            return max + 1 == MIDI_PERCUSSION_CHAN ? max + 2 : max + 1;
        }

        private static byte GetMIDIChannel(int mus_channel)
        {
            // Find the MIDI channel to use for this MUS channel.
            // MUS channel 15 is the percusssion channel.

            if (mus_channel == MUS_PERCUSSION_CHAN)
            {
                return MIDI_PERCUSSION_CHAN;
            }
            else
            {
                // If a MIDI channel hasn't been allocated for this MUS channel
                // yet, allocate the next free MIDI channel.

                if (channel_map[mus_channel] == -1)
                {
                    channel_map[mus_channel] = AllocateMIDIChannel();
                }

                return (byte)channel_map[mus_channel];
            }
        }

        private static bool ReadMusHeader(Stream Input, musheader Header)
        {
            Input.Read(Header.id, 0, 4);
            Header.scorelength = GetShort(Input);
            Header.scorestart = GetShort(Input);
            Header.primarychannels = GetShort(Input);
            Header.secondarychannels = GetShort(Input);
            Header.instrumentcount = GetShort(Input);

            //TODO: Convert to little endian? GetShort is prepared!

            return true;
        }

        private static ushort GetShort(Stream Input)
        {
            return GetShort(Input, false);
        }

        private static ushort GetShort(Stream Input, bool Swap)
        {
            byte[] b = new byte[4];
            Input.Read(b, 0, 2);
            b[3] = b[0];
            b[2] = b[1];
            //use "2" to return swapped order
            return BitConverter.ToUInt16(b, Swap ? 2 : 0);
        }
    }
}
