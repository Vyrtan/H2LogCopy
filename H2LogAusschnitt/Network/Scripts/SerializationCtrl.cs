using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Reflection;
using System.ComponentModel;

public class SerializationCtrl
{
	// the ID for all unknown types. All regular types have positive ID's.
    public const int UNKNOWN_TYPE_ID = -1;

	// maps types to serializers which will take care of serialization of objects of that Type.
    private static readonly Dictionary<Type, Serializer> typeMap = new Dictionary<Type, Serializer>();
	// maps from a type-ID to a helper object with useful information
    private static readonly Dictionary<int, TypeHelpTuple> typeIdMap = new Dictionary<int, TypeHelpTuple>();
	// the internal error buffer
    private static readonly List<Error> errBuffer = new List<Error>();
    /// <summary>
    /// This will return a List of all Errors which appeared since the last call to ClearCurrentErrors().
    /// 
    /// The methods of the SerializationCtrl don't throw exceptions. Instead the push error messages to 
    /// an internal error buffer. This method will return the internal error buffer. The error buffer 
    /// is not immutable but should not be written to from the outside.
    /// 
    /// To get the error message from an Error object use the methods GetFormatString() and GetFormatArgs()
    /// in a printf like output method.
    /// </summary>
    public static List<Error> GetCurrentErrors()
    {
        return errBuffer;
    }
    /// <summary>
    /// This will clear the error buffer retuned by the GetCurrentErrors() method.
    /// For more details on error retrievel see the documentation of GetCurrentErrors().
    /// </summary>
    public static void ClearCurrentErrors()
    {
        errBuffer.Clear();
    }
    /// <summary>
    /// Registers all default serializers for the most commonly used types.
    /// </summary>
    static SerializationCtrl()
    {
        RegisterSerializer(typeof(byte[]), new ArraySerializer());
        RegisterSerializer(typeof(int), new IntSerializer());
        RegisterSerializer(typeof(int[]), new ArraySerializer());
        RegisterSerializer(typeof(long), new LongSerializer());
        RegisterSerializer(typeof(long[]), new ArraySerializer());
        RegisterSerializer(typeof(float), new FloatSerializer());
        RegisterSerializer(typeof(float[]), new ArraySerializer());
        RegisterSerializer(typeof(double), new DoubleSerializer());
        RegisterSerializer(typeof(double[]), new ArraySerializer());
        RegisterSerializer(typeof(bool), new BoolSerializer());
        RegisterSerializer(typeof(bool[]), new ArraySerializer());
        RegisterSerializer(typeof(string), new StringSerializer());
        RegisterSerializer(typeof(string[]), new ArraySerializer());
        RegisterSerializer(typeof(object), new ObjectSerializer());
        RegisterSerializer(typeof(object[]), new ArraySerializer());
        RegisterSerializer(typeof(List<object>), new ListSerializer());
        RegisterSerializer(typeof(List<string>), new ListSerializer());
    }
    /// <summary>
    /// Registers a custom serializer for the given type.
    /// If the given type already has a serializer registered an Error will be 
    /// generated and the serializer will not be changed.
    /// </summary>
    /// <param name="type">The Type for which the serializer should be registered.</param>
    /// <param name="ser">The serializer used to turn objects of the give type to 
	/// byte arrays.</param>
    public static void RegisterSerializer(Type type, Serializer ser)
    {
        if (typeMap.ContainsKey(type)) {
            errBuffer.Add(new ErrorTypeSerializerConflict(type));
            return;
        }
        ser.id = typeMap.Count;
        typeMap.Add(type, ser);
        typeIdMap.Add(ser.id, new TypeHelpTuple(type, ser));
    }
    /// <summary>
    /// Returns the type-ID for the given object.
    /// The type-ID is used to distinguish between serialized objects.
    /// </summary>
    /// <param name="obj">The object for which the type-ID is returned.</param>
    public static int GetSerializeID(object obj)
    {
        return GetSerializeID(obj.GetType());
    }
    /// <summary>
    /// Returns the ID for the given Type.
    /// The type-ID is used to distinguish between serialized objects.
    /// This is the opposite operation of GetTypeBySerializeID(int).
    /// </summary>
    /// <param name="type">The Type for which the ID is returned.</param>
    public static int GetSerializeID(Type type)
    {
        Serializer ser;
        bool isKnownType = typeMap.TryGetValue(type, out ser);
        return isKnownType ? ser.id : UNKNOWN_TYPE_ID;
    }
    /// <summary>
    /// Returns the Type for a given type-ID.
    /// This is the opposite operation of GetSerializeID(Type).
    /// </summary>
    /// <param name="serializeID">The type-ID for which a registered Type is returned.</param>
    public static Type GetTypeBySerializeID(int serializeID)
    {
        if (serializeID == UNKNOWN_TYPE_ID)
        {
            return null;
        }
        TypeHelpTuple typeTuple;
        if (typeIdMap.TryGetValue(serializeID, out typeTuple))
        {
            return typeTuple.type;
        }
        return null;
    }
    /// <summary>
    /// Takes an object, serializes it and writes its byte data into a new List of bytes.
    /// Calling this method is equivalent to calling Serialize(List<byte>, object) with 
    /// a newly created List.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    public static List<byte> Serialize(object obj)
    {
        List<byte> bytes = new List<byte>();
        Serialize(bytes, obj);
        return bytes;
    }
    /// <summary>
    /// Serializes all objects as one object graph and writes their serialized bytes 
    /// into the given List of bytes. The objects are serialized one after another as 
    /// returned by the Lists iterator.
    /// For more details see the documentation of Serialize(SerializationData, object).
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    public static void Serialize(List<byte> bytes, List<object> objects)
    {
        SerializationData data = new SerializationData();
        data.bytes = bytes;
        foreach (var obj in objects)
        {
            Serialize(data, obj);
        }
    }
    /// <summary>
    /// Takes an object, serializes it and writes its byte data into the given List of bytes.
    /// For more details see the documentation of Serialize(SerializationData, object).
    /// </summary>
    /// <param name="bytes">The serialized bytes will be added to this List.</param>
    /// <param name="obj">The object to serialize.</param>
    public static void Serialize(List<byte> bytes, object obj)
    {
        SerializationData data = new SerializationData();
        data.bytes = bytes;
        Serialize(data, obj);
    }
    /// <summary>
    /// Takes an object, serializes it and writes its byte data into the given data object.
    /// How the byte data looks like depends on the object given:
    /// 	if obj is null:
    /// 		a single byte equal to ChunkType.NULL
    /// 	if the obj was already written to the serialization data before:
    /// 		a single byte equal to ChunkType.ID_REF
    /// 		2 bytes containing the objects ID. The ID is read from the objToID map in data
    /// 	if the type of the obj is not known:
    /// 		a single byte equal to ChunkType.ID_REF
    /// 		3 bytes equal to UNKNOWN_TYPE_ID
    /// 	if the type of the obj is a primitive type:
    /// 		a single byte equal to ChunkType.PRIMITIVE
    /// 		3 bytes with the type-ID of the primitive
    /// 		the serializer for the primitives type will be used to write all following bytes
    /// 	if the type of the obj is a known non-primitive type:
    /// 		a single byte equal to ChunkType.NEW_REF
    /// 		3 bytes with the type-ID of the object
    /// 		the ref-ID of the object will not be written. It is determined 
    /// 		deterministically from the order of writes / reads.
    /// 		the serializer for the objects type will be used to write all following bytes
    /// 
    /// Attempting to write an object for which no serializer is known will result in an 
    /// error being written to the internal error buffer.
    /// For more information on the error handling process refer to the documentation of 
    /// GetErrorBuffer().
    /// </summary>
    /// <param name="data">The serialized data used during this serialization process.</param>
    /// <param name="obj">The object to serialize.</param>
    public static void Serialize(SerializationData data, object obj)
    {
        if (obj == null)
        {
			// only write a single byte
            data.WriteChunkType(ChunkType.NULL);
            return;
        }
        short objID;
        bool hasID = data.objToID.TryGetValue(obj, out objID);
		// whether or not the same object was written before to the same data instance
        if (hasID)
        {
			// only write 3 bytes
            data.WriteChunkType(ChunkType.ID_REF);
            data.WriteInt2B(objID);
            return;
        }

        Type type = obj.GetType();
        Serializer ser;
        bool isKnownType = typeMap.TryGetValue(type, out ser);
		// whether or not a known serializer exists for the given object type
        if (!isKnownType)
        {
			// only write 4 bytes
            data.WriteChunkType(ChunkType.NEW_REF);
            data.WriteInt3B(UNKNOWN_TYPE_ID);

            errBuffer.Add(new ErrorSerializeUnknownType(obj));
            return;
        }
		// whether the data is a primitive (int, float, double, boolean, etc)
        if (ser.IsPrimitive())
        {
			// primitives write at least 4 bytes, this is the first
            data.WriteChunkType(ChunkType.PRIMITIVE);
        }
        else
        {
			// this will register this object as being written to the given data 
			// for the first time. If written to this data again in the future the 
			// object will be serialized with less space requirements.
            data.RegisterObject(obj);
			// non-primitives write at least 4 bytes, this is the first
            data.WriteChunkType(ChunkType.NEW_REF);
        }
		// writes the 3 byte type-ID of the object as returned by GetTypeID(object)
        data.WriteInt3B(ser.id);
		// delegates further serialization to the serializer registered for the objects type
        ser.WriteBytes(data, obj);
    }
    /// <summary>
    /// Takes a byte array, attempts to deserializes it and returns a List of deserialized objects.
    /// Creates a new List on each call.
    /// Calling this method is equivalent to calling Deserialize(byte[], List<object>) with a new, 
    /// empty List.
    /// </summary>
    /// <param name="bytes">Bytes previously serialized by the SerializationCtrl.</param>
    public static List<object> Deserialize(byte[] bytes)
    {
        List<object> output = new List<object>();
        return Deserialize(bytes, output);
    }
    /// <summary>
    /// Takes a byte array, attempts to deserializes it and adds each deserialized 
    /// object to the given output List.
    /// </summary>
    /// <param name="bytes">Bytes previously serialized by the SerializationCtrl.</param>
    /// <param name="output">A List to which deserialized objects will be added.</param>
    public static List<object> Deserialize(byte[] bytes, List<object> output)
    {
        DeserializationData data = new DeserializationData();
        data.bytes = bytes;
        data.offset = 0;

        while (data.offset < bytes.Length)
        {
            object obj = Deserialize(data);
            output.Add(obj);
        }
        return output;
    }
    /// <summary>
    /// Takes a byte array and attempts to deserialize a single object at the given offset.
    /// The deserialized object will be returned.
    /// </summary>
    /// <param name="bytes">Bytes previously serialized by the SerializationCtrl.</param>
    /// <param name="offset">An index within the array from which deserialization will be attempted.</param>
    public static object Deserialize(byte[] bytes, int offset)
    {
        DeserializationData data = new DeserializationData();
        data.bytes = bytes;
        data.offset = offset;
        return Deserialize(data);
    }
    /// <summary>
    /// Takes DeserializationData which includes a byte array and an offset into the array 
    /// from where to start deserializing and attempts to deserialize a single object.
    /// The deserialized object or null will be returned.
    /// 
    /// For more details on the serialization format refer to the documentation of the 
    /// Serialize(SerializationData, object) method.
    /// 
    /// This method may push errors to the internal error buffer when attempting to deserialize 
    /// objects of unknown type, illegal data or data not generated by the SerializationCtrl. 
    /// By the nature of the serialization process attempting to deserialize data not generated 
    /// by the SerializationCtrl, manually modifying the raw data, providing an illegal offset 
    /// or garbage data will result in undefined behavior. Any random byte array might randomly 
    /// happen to be identical to valid, serialized data and is indistinguishable from actual data.
    /// For more information on the error handling process refer to the documentation of 
    /// GetErrorBuffer().
    /// 
    /// The following return values are possible:
    /// 	if the serialized data is illegal
    /// 		null is returned and an error is generated
    /// 	if the serialized data was unknown to the serializer
    /// 		null is returned and an error is generated
    /// 	if the serialized data is unknown to the deserializer
    /// 		null is returned and an error is generated
    /// 	else
    /// 		the deserialized object (or null) is returned
    /// </summary>
    /// <param name="data">The serialized data used during this deserialization process.</param>
    public static object Deserialize(DeserializationData data)
    {
        ChunkType type = data.ReadChunkType();
		// if something went wrong with the serialization process
        if (type == ChunkType.ILLEGAL) {
            errBuffer.Add(new ErrorDeserializeIllegalChunk());
            return null;
        }
		// the serialized "object" was null
        if (type == ChunkType.NULL)
        {
            return null;
        }
		// the deserializd object was already read before
        if (type == ChunkType.ID_REF)
        {
            short objRefID = data.ReadInt2B();
            object refObj;
            bool success = data.objToID.TryGetValue(objRefID, out refObj);
            if (success)
            {
                return refObj;
            }
			// the serialized data is corrupted
            errBuffer.Add(new ErrorDeserializeUnknownObjectRef(objRefID));
            return null;
        }
        int typeID = data.ReadInt3B();
        if (typeID == UNKNOWN_TYPE_ID)
        {
            errBuffer.Add(new ErrorDeserializeUnknownType(typeID));
            return null;
        }

        TypeHelpTuple typeTuple;
        bool isKnownType = typeIdMap.TryGetValue(typeID, out typeTuple);
        // this can happen if the SerializationCtrl on both devices has desynchronized type IDs.
        if (!isKnownType)
        {
            errBuffer.Add(new ErrorDeserializeUnknownType(typeID));
            return null;
        }
        Serializer ser = typeTuple.ser;
        short objID = 0;
        if (!ser.IsPrimitive())
        {
            objID = data.nextID++;
        }
        object obj = ser.CreateNewInstance(data, typeTuple.type);
        if (!ser.IsPrimitive())
        {
            data.RegisterObject(obj, objID);
        }
        ser.ReadBytes(data, obj);
        return obj;
    }
    /// <summary>
    /// Sub classes of this class define how objects of a certain Type are supposed to be 
    /// serialized and deserialized. Custom implementations can be registered at the 
    /// SerializationCtrl with the RegisterSerializer(Type, Serializer) method.
    /// 
    /// Default implementations for most primitive types and basic objects are provided.
    /// </summary>
    public abstract class Serializer
    {
		// this is set by the SerializationCtrl and should never be written to by the user
        public int id;
		// when this returns true, objects of read / written by this Serializer will never 
		// be treated like primitives and never be cached / reused.
        public virtual bool IsPrimitive()
        {
            return false;
        }
		// Serializes the given object and writes it to the byte array in data.
        public virtual void WriteBytes(SerializationData data, object obj) { }
		// Deserializes the bytes from data and creates a new instance of the given type
        public virtual object CreateNewInstance(DeserializationData data, Type type) { return null; }
		// Deserializes the bytes from data and modifies the given object accordingly
        public virtual void ReadBytes(DeserializationData data, object obj) { }
    }
    /// <summary>
    /// Does not read or write any data about an object but simply creates a new 
    /// Instance every time data is read.
    /// </summary>
    public class ObjectSerializer : Serializer
    {
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            return Activator.CreateInstance(type);
        }
    }
    /// <summary>
    /// Reads and writes regular 4 byte int's.
    /// </summary>
    public class IntSerializer : Serializer
    {
        public override bool IsPrimitive()
        {
            return true;
        }
        public override void WriteBytes(SerializationData data, object obj)
        {
            int i = (int)obj;
            data.WriteInt4B(i);
        }
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            return data.ReadInt4B();
        }
    }
    /// <summary>
    /// Reads and writes regular 8 byte int's.
    /// </summary>
    public class LongSerializer : Serializer
    {
        public override bool IsPrimitive()
        {
            return true;
        }
        public override void WriteBytes(SerializationData data, object obj)
        {
            long i = (long)obj;
            data.WriteInt8B(i);
        }
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            return data.ReadInt8B();
        }
    }
    /// <summary>
    /// Reads and writes regular 8 byte doubles's.
    /// </summary>
    public class DoubleSerializer : Serializer
    {
        public override bool IsPrimitive()
        {
            return true;
        }
        public override void WriteBytes(SerializationData data, object obj)
        {
            double d = (double)obj;
            byte[] arr = BitConverter.GetBytes(d);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(arr);
            }
            for (int i = 0; i < arr.Length; i++)
            {
                data.bytes.Add(arr[i]);
            }
        }
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            byte[] arr = new Byte[sizeof(double)];
            for (int i = 0; i < sizeof(double); i++)
            {
                arr[i] = data.bytes[data.offset++];
            }
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(arr);
            }
            return BitConverter.ToDouble(arr, 0);
        }
    }
    /// <summary>
    /// Reads and writes regular 4 byte floats's.
    /// </summary>
    public class FloatSerializer : Serializer
    {
        public override bool IsPrimitive()
        {
            return true;
        }
        public override void WriteBytes(SerializationData data, object obj)
        {
            float d = (float)obj;
            byte[] arr = BitConverter.GetBytes(d);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(arr);
            }
            for (int i = 0; i < arr.Length; i++)
            {
                data.bytes.Add(arr[i]);
            }
        }
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            byte[] arr = new Byte[sizeof(float)];
            for (int i = 0; i < sizeof(float); i++)
            {
                arr[i] = data.bytes[data.offset++];
            }
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(arr);
            }
            return BitConverter.ToSingle(arr, 0);
        }
    }
    /// <summary>
    /// Reads and writes regular single byte booleans's.
    /// </summary>
    public class BoolSerializer : Serializer
    {
        public override bool IsPrimitive()
        {
            return true;
        }
        const byte bTrue = 1;
        const byte bFalse = 0;
        public override void WriteBytes(SerializationData data, object obj)
        {
            bool i = (bool)obj;
            data.bytes.Add(i ? bTrue : bFalse);
        }
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            byte i = data.bytes[data.offset++];
            return i != bFalse;
        }
    }
    /// <summary>
    /// Reads and writes Strings in utf8 encoding.
    /// </summary>
    public class StringSerializer : Serializer
    {
        private Encoding utf8 = Encoding.UTF8;
        public override void WriteBytes(SerializationData data, System.Object obj)
        {
            string str = (string)obj;
            byte[] strBytes = utf8.GetBytes(str);

            data.WriteInt4B(strBytes.Length);
            for (int i = 0; i < strBytes.Length; i++)
            {
                data.bytes.Add(strBytes[i]);
            }
        }
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            int byteCount = data.ReadInt4B();

            byte[] strBytes = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                strBytes[i] = data.bytes[data.offset + i];
            }
            data.offset += byteCount;
            return utf8.GetString(strBytes.ToArray());
        }
    }
    /// <summary>
    /// Reads and writes any kind of array.
    /// </summary>
    public class ArraySerializer : Serializer
    {
        public override void WriteBytes(SerializationData data, object obj)
        {
            Array arr = (Array)obj;
            Type elementType = arr.GetType().GetElementType();
            int elementTypeID = SerializationCtrl.GetSerializeID(elementType);
            int arrLength = arr.GetLength(0);
            data.WriteInt4B(elementTypeID);
            data.WriteInt4B(arrLength);

            long[] indices = { 0 };
            for (int i = 0; i < arrLength; i++)
            {
                indices[0] = i;
                object val = arr.GetValue(indices);
                SerializationCtrl.Serialize(data, val);
            }
        }
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            int elementTypeID = data.ReadInt4B();
            Type elementType = GetTypeBySerializeID(elementTypeID);
            int arrLength = data.ReadInt4B();
            Array arr = Array.CreateInstance(elementType, arrLength);
            return arr;
        }
        public override void ReadBytes(DeserializationData data, object obj)
        {
            Array arr = (Array) obj;

            long[] indices = { 0 };
            for (int i = 0; i < arr.Length; i++)
            {
                indices[0] = i;
                object val = SerializationCtrl.Deserialize(data);
                arr.SetValue(val, indices);
            }
        }
    }
    /// <summary>
    /// Reads and writes any kind of IList.
    /// </summary>
    public class ListSerializer : Serializer
    {
        public override void WriteBytes(SerializationData data, object obj)
        {
            IList list = (IList)obj;
            int count = list.Count;
            data.WriteInt4B(count);

            for (int i = 0; i < count; i++)
            {
                object val = list[i];
                SerializationCtrl.Serialize(data, val);
            }
        }
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            return Activator.CreateInstance(type);
        }
        public override void ReadBytes(DeserializationData data, object obj)
        {
            IList list = (IList)obj;
            int count = data.ReadInt4B();

            for (int i = 0; i < count; i++)
            {
                object val = SerializationCtrl.Deserialize(data);
                list.Add(val);
            }
        }
    }
    /// <summary>
    /// Uses reflection to read and write most kinds of objects.
    /// If an object can not be serialized by this implementation a custom 
    /// implementation is probably required.
    /// </summary>
    public class ClassSerializer : Serializer
    {
        public override void WriteBytes(SerializationData data, object obj)
        {
            FieldInfo[] props = obj.GetType().GetFields(
                BindingFlags.NonPublic
                | BindingFlags.Public
                | BindingFlags.Instance);

            foreach (var info in props)
            {
                object val = info.GetValue(obj);
                SerializationCtrl.Serialize(data, val);
            }
        }
        public override object CreateNewInstance(DeserializationData data, Type type)
        {
            return Activator.CreateInstance(type);
        }
        public override void ReadBytes(DeserializationData data, object obj)
        {
            FieldInfo[] props = obj.GetType().GetFields(
                BindingFlags.NonPublic
                | BindingFlags.Public
                | BindingFlags.Instance);

            foreach (var info in props)
            {
                object val = SerializationCtrl.Deserialize(data);
                info.SetValue(obj, val);
            }
        }
    }
    /// <summary>
    /// This class is used internally to map Types to Serializers.
    /// </summary>
    public class TypeHelpTuple
    {
        public readonly Type type;
        public readonly Serializer ser;

        public TypeHelpTuple(Type type, Serializer ser)
        {
            this.type = type;
            this.ser = ser;
        }
    }
    /// <summary>
    /// Identifying bytes for Chunks of byte data.
    /// These bytes are always written as the very first byte in a chunk of data.
    /// </summary>
    public enum ChunkType
    {
        ILLEGAL     = 11, // data is illegal, can not be deserialized
        NULL        = 22, // data is a null reference
        PRIMITIVE   = 33, // data is primitive object
        ID_REF      = 44, // data is an object already read / written before
        NEW_REF     = 55, // data is an object read / written for the first time
    }
    /// <summary>
    /// Used internally to store the state of a serialization process and any 
    /// intermediary data.
    /// </summary>
    public class SerializationData
    {
		// maps object to a Ref-ID for objects that have already been written
        public Dictionary<object, short> objToID = new Dictionary<object, short>();
		// the written bytes
        public List<byte> bytes;

		// Creates a new, unique Ref-ID for the given object and saves it internally
        public void RegisterObject(object obj)
        {
            short id = (short) objToID.Count;
            objToID.Add(obj, id);
        }
        
        public void WriteInt2B(short value)
        {
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value >> 0));
        }

        public void WriteInt3B(int value)
        {
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value >> 0));
        }

        public void WriteInt4B(int value)
        {
            bytes.Add((byte)(value >> 24));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value >> 0));
        }

        public void WriteInt8B(long value)
        {
            bytes.Add((byte)(value >> 56));
            bytes.Add((byte)(value >> 48));
            bytes.Add((byte)(value >> 40));
            bytes.Add((byte)(value >> 32));
            bytes.Add((byte)(value >> 24));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value >> 0));
        }

        public void WriteChunkType(ChunkType type)
        {
            bytes.Add((byte) type);
        }
    }
    /// <summary>
    /// Used internally to store the state of a deserialization process and any 
    /// intermediary data.
    /// </summary>
    public class DeserializationData
    {
        public Dictionary<short, object> objToID = new Dictionary<short, object>();
        public byte[] bytes;
        public int offset = 0;
        public short nextID = 0;

        public void RegisterObject(object obj, short id)
        {
            objToID.Add(id, obj);
        }

        public int ReadInt8B()
        {
            byte b1 = bytes[offset + 0];
            byte b2 = bytes[offset + 1];
            byte b3 = bytes[offset + 2];
            byte b4 = bytes[offset + 3];
            byte b5 = bytes[offset + 4];
            byte b6 = bytes[offset + 5];
            byte b7 = bytes[offset + 6];
            byte b8 = bytes[offset + 7];
            offset += 8;
            return b8 & 0xFF | (b7 & 0xFF) << 8 | (b6 & 0xFF) << 16 | (b5 & 0xFF) << 24 
                | (b4 & 0xFF) << 32 | (b3 & 0xFF) << 40 | (b2 & 0xFF) << 48 | (b1 & 0xFF) << 56;
        }

        public int ReadInt4B()
        {
            byte b1 = bytes[offset + 0];
            byte b2 = bytes[offset + 1];
            byte b3 = bytes[offset + 2];
            byte b4 = bytes[offset + 3];
            offset += 4;
            return b4 & 0xFF | (b3 & 0xFF) << 8 | (b2 & 0xFF) << 16 | (b1 & 0xFF) << 24;
        }

        public int ReadInt3B()
        {
            byte b2 = bytes[offset++];
            byte b3 = bytes[offset++];
            byte b4 = bytes[offset++];
            return b4 & 0xFF | (b3 & 0xFF) << 8 | (b2 & 0xFF) << 16;// | (b1 & 0xFF) << 24;
        }

        public short ReadInt2B()
        {
            byte b3 = bytes[offset++];
            byte b4 = bytes[offset++];
            return (short) (b4 & 0xFF | (b3 & 0xFF) << 8);
        }

        public ChunkType ReadChunkType()
        {
            byte chunkByte = bytes[offset++];
            foreach (var type in Enum.GetValues(typeof(ChunkType)).Cast<ChunkType>())
            {
                if ((byte) type == chunkByte) {
                    return type;
                }
            }
            return ChunkType.ILLEGAL;
        }
    }

    public interface Error
    {
        string GetFormatString();

        object[] GetFormatArgs();
    }

    private class ErrorSerializeUnknownType : Error
    {
        private readonly object[] args = new object[2];
        public ErrorSerializeUnknownType(object obj)
        {
            args[0] = obj;
            args[1] = obj.GetType();
        }

        public string GetFormatString()
        {
            return "Attempted to serialize an unknown object type. Type={0}, Object={1}";
        }

        public object[] GetFormatArgs()
        {
            return args;
        }
    }

    private class ErrorDeserializeIllegalChunk : Error
    {
        public string GetFormatString()
        {
            return "Attempted to deserialize an unknown chunk type.";
        }

        public object[] GetFormatArgs()
        {
            return new object[0];
        }
    }

    private class ErrorDeserializeUnknownType : Error
    {
        private readonly object[] args = new object[1];
        public ErrorDeserializeUnknownType(int typeID)
        {
            args[0] = typeID;
        }

        public string GetFormatString()
        {
            return "Attempted to deserialize an unknown object type. Type-ID={0}";
        }

        public object[] GetFormatArgs()
        {
            return args;
        }
    }

    private class ErrorTypeSerializerConflict : Error
    {
        private readonly object[] args = new object[1];

        public ErrorTypeSerializerConflict(Type type)
        {
            args[0] = type;
        }

        public string GetFormatString()
        {
            return "Registered the same Type two times (or more) for serialization. Type={0}";
        }

        public object[] GetFormatArgs()
        {
            return args;
        }
    }

    private class ErrorDeserializeUnknownObjectRef : Error
    {
        private readonly object[] args = new object[1];
        public ErrorDeserializeUnknownObjectRef(int typeID)
        {
            args[0] = typeID;
        }

        public string GetFormatString()
        {
            return "Attempted to deserialize an unknown object reference. Ref-ID={0}";
        }

        public object[] GetFormatArgs()
        {
            return args;
        }
    }
}