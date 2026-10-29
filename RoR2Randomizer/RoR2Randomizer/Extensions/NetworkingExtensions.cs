using HG;
using RoR2;
using System;
using Unity;
using UnityEngine;
using UnityEngine.Networking;

namespace RoR2Randomizer.Extensions
{
    public static class NetworkingExtensions
    {
        public static SerializableSystemType ReadSerializableType(this NetworkReader reader)
        {
            return new SerializableSystemType() { assemblyQualifiedName = reader.ReadString() };
        }
        public static void WriteSerializableType(this NetworkWriter writer, SerializableSystemType serializableSystemType)
        {
            writer.Write(serializableSystemType.assemblyQualifiedName);
        }

        static T? readNullable<T>(this NetworkReader reader, Func<NetworkReader, T> readFunc) where T : struct
        {
            return reader.readNullable(() => readFunc(reader));
        }
        static T? readNullable<T>(this NetworkReader reader, Func<T> readFunc) where T : struct
        {
            return reader.ReadBoolean() ? readFunc() : null;
        }

        static void writeNullable<T>(this NetworkWriter writer, T? value, Action<NetworkWriter, T> writeFunc) where T : struct
        {
            writer.writeNullable(value, t => writeFunc(writer, t));
        }
        static void writeNullable<T>(this NetworkWriter writer, T? value, Action<T> writeFunc) where T : struct
        {
            if (value.HasValue)
            {
                writer.Write(true);
                writeFunc(value.Value);
            }
            else
            {
                writer.Write(false);
            }
        }

        public static DamageType? ReadNullableDamageType(this NetworkReader reader)
        {
            return reader.readNullable(r => (DamageType)r.ReadInt32());
        }
        public static void WriteNullableDamageType(this NetworkWriter writer, DamageType? damageType)
        {
            writer.writeNullable(damageType, (w, v) => w.Write((int)v));
        }

        public static Vector3? ReadNullableVector3(this NetworkReader reader)
        {
            return reader.readNullable(reader.ReadVector3);
        }
        public static void WriteNullableVector3(this NetworkWriter writer, Vector3? vector3)
        {
            writer.writeNullable(vector3, writer.Write);
        }

        public static MasterCatalog.NetworkMasterIndex? ReadNullableNetworkMasterIndex(this NetworkReader reader)
        {
            return reader.readNullable(GeneratedNetworkCode._ReadNetworkMasterIndex_MasterCatalog);
        }
        public static void WriteNullableNetworkMasterIndex(this NetworkWriter writer, MasterCatalog.NetworkMasterIndex? masterIndex)
        {
            writer.writeNullable(masterIndex, GeneratedNetworkCode._WriteNetworkMasterIndex_MasterCatalog);
        }
    }
}
