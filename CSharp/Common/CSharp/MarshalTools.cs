using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;

namespace CSharp
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct SafeArray
    {
        public IntPtr Data;
        public int Size;
        public bool Owns;

        public static T[] __FromPInvoke<T>(SafeArray data, bool ownsNativeInstance)
        {
            if (data.Size == 0)
                return new T[0];

            var type = typeof(T);
            T[] result;
            if (type.IsValueType)
            {
                // Array of structs or builtin types.
                var size = Marshal.SizeOf<T>();
                result = new T[data.Size / size];
                var handle = GCHandle.Alloc(result, GCHandleType.Pinned);
                Buffer.MemoryCopy((void*)data.Data, (void*)handle.AddrOfPinnedObject(), data.Size, data.Size);
                handle.Free();
            }
            else
            {
                // Array of pointers to objects
                var tFromPInvoke = type.GetMethod("__FromPInvoke", BindingFlags.NonPublic | BindingFlags.Static);
                var pointers = (void**) data.Data;
                Debug.Assert(pointers != null);
                Debug.Assert(tFromPInvoke != null);
                var count = data.Size / IntPtr.Size;
                result = new T[count];
                for (int i = 0; i < count; i++)
                    result[i] = (T)tFromPInvoke.Invoke(null, new object[]{new IntPtr(pointers[i]), false});
            }

            if (data.Owns)
                MarshalTools.c_free(data.Data);

            return result;
        }

        public static SafeArray __ToPInvoke<T>(T[] data)
        {
            if (data == null)
                return new SafeArray();

            var type = typeof(T);
            var result = new SafeArray {Owns = true};

            if (type.IsValueType)
            {
                // Array of structs or builtin types.
                result.Size = data.Length * Marshal.SizeOf<T>();
                result.Data = MarshalTools.c_alloc(result.Size);
                var handle = GCHandle.Alloc(result, GCHandleType.Pinned);
                Buffer.MemoryCopy((void*)handle.AddrOfPinnedObject(), (void*)result.Data, result.Size, result.Size);
                handle.Free();
            }
            else
            {
                // Array of pointers to objects
                result.Size = data.Length * IntPtr.Size;
                result.Data = MarshalTools.c_alloc(result.Size);
                var tToPInvoke = type.GetMethod("__ToPInvoke", BindingFlags.NonPublic | BindingFlags.Static);
                var pointers = (void**) result.Size;
                Debug.Assert(tToPInvoke != null);
                Debug.Assert(pointers != null);
                for (var i = 0; i < data.Length; i++)
                    pointers[i] = ((IntPtr) tToPInvoke.Invoke(null, new object[] {data[i]})).ToPointer();
            }

            return result;
        }
    }

    internal static class MarshalTools
    {
        [DllImport(Config.NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void c_free(IntPtr ptr);
        [DllImport(Config.NativeLibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr c_alloc(int size);

        internal static string GetUtf8String(IntPtr ptr)
        {
            var len = 0;
            while (Marshal.ReadByte(ptr, len) != 0)
                len++;
            var buffer = new byte[len];
            Marshal.Copy(ptr, buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer);
        }

        internal static string GetUtf8StringFree(IntPtr ptr)
        {
            string result = GetUtf8String(ptr);
            c_free(ptr);
            return result;
        }

        internal static bool HasOverride(this MethodInfo method)
        {
            return (method.Attributes & MethodAttributes.Virtual) != 0 &&
                   (method.Attributes & MethodAttributes.NewSlot) == 0;
        }

        internal static bool HasOverride(this Type type, string methodName, params Type[] paramTypes)
        {
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                CallingConventions.HasThis, paramTypes, new ParameterModifier[0]);
            return method != null && method.HasOverride();
        }
    }
}
