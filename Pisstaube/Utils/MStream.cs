using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Pisstaube.Utils
{
    public interface ISerializer
    {
        void ReadFromStream (MStreamReader sr);
        void WriteToStream (MStreamWriter sw);
    }

    public class MStreamWriter : BinaryWriter
    {
        public MStreamWriter (Stream s) : base (s, Encoding.UTF8)
        { }

        public long Length => BaseStream.Length;

        public static MStreamWriter New ( ) => new MStreamWriter (new MemoryStream ( ));

        public void Write (ISerializer seri)
        {
            seri.WriteToStream (this);
        }

        public void Write (BinaryWriter w)
        {
            w.BaseStream.Position = 0;
            w.BaseStream.CopyTo (BaseStream);
        }

        public void Write (string value, bool nullable)
        {
            if (value == null && nullable)
            {
                base.Write ((byte) 0);
            }
            else
            {
                base.Write ((byte) 0x0b);
                base.Write (value + "");
            }
        }

        public override void Write (string value)
        {
            Write ((byte) 0x0b);
            base.Write (value);
        }

        public override void Write (byte[ ] buff)
        {
            var length = buff.Length;
            Write (length);
            if (length > 0)
                base.Write (buff);
        }

        public void Write (List<int> list)
        {
            var count = (short) list.Count;
            Write (count);
            for (var i = 0; i < count; i++)
                Write (list[i]);
        }

        public void WriteRawBuffer (byte[ ] buff)
        {
            base.Write (buff);
        }

        public void WriteRawString (string value)
        {
            WriteRawBuffer (Encoding.UTF8.GetBytes (value));
        }

        public void WriteObject (object obj)
        {
            if (obj == null)
                Write ((byte) 0x00);
            else
                switch (obj.GetType ( ).Name)
                {
                    case "Boolean":
                        Write ((bool) obj);
                        break;
                    case "Byte":
                        Write ((byte) obj);
                        break;
                    case "UInt16":
                        Write ((ushort) obj);
                        break;
                    case "UInt32":
                        Write ((uint) obj);
                        break;
                    case "UInt64":
                        Write ((ulong) obj);
                        break;
                    case "SByte":
                        Write ((sbyte) obj);
                        break;
                    case "Int16":
                        Write ((short) obj);
                        break;
                    case "Int32":
                        Write ((int) obj);
                        break;
                    case "Int64":
                        Write ((long) obj);
                        break;
                    case "Char":
                        Write ((char) obj);
                        break;
                    case "String":
                        Write ((string) obj);
                        break;
                    case "Single":
                        Write ((float) obj);
                        break;
                    case "Double":
                        Write ((double) obj);
                        break;
                    case "Decimal":
                        Write ((decimal) obj);
                        break;
                    default:
                        var b = new BinaryFormatter
                        {
                            AssemblyFormat = FormatterAssemblyStyle.Simple,
                            TypeFormat = FormatterTypeStyle.TypesWhenNeeded
                        };
                        b.Serialize (BaseStream, obj);
                        break;
                }
        }

        public byte[ ] ToArray ( ) => ((MemoryStream) BaseStream).ToArray ( );
    }

    public class MStreamReader : BinaryReader
    {
        public MStreamReader (Stream s) : base (s, Encoding.UTF8)
        { }

        public override string ReadString ( ) => (ReadByte ( ) == 0x00 ? "" : base.ReadString ( )) ??
            throw new InvalidOperationException ( );

        public byte[ ] ReadBytes ( )
        {
            var len = ReadInt32 ( );
            return len > 0 ? base.ReadBytes (len) : len < 0 ? null : new byte[0];
        }

        public List<int> ReadInt32List ( )
        {
            var count = ReadInt16 ( );
            if (count < 0)
                return new List<int> ( );
            var outList = new List<int> (count);
            for (var i = 0; i < count; i++)
                outList.Add (ReadInt32 ( ));
            return outList;
        }

        public T ReadData<T> ( ) where T : ISerializer, new ( )
        {
            var data = new T ( );
            data.ReadFromStream (this);
            return data;
        }

        public byte[ ] ReadToEnd ( )
        {
            var x = new List<byte> ( );
            while (BaseStream.Position != BaseStream.Length)
                x.Add (ReadByte ( ));

            return x.ToArray ( );
        }
    }
}
