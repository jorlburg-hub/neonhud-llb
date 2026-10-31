using System;
using System.Collections.Generic;

namespace UnityEngine
{
    [Flags] public enum HideFlags { None = 0, DontUnloadUnusedAsset = 32, HideAndDontSave = 61 }

    public class Object
    {
        internal bool destroyed;
        public static readonly List<Object> All = new List<Object>();
        public Object() { All.Add(this); }
        public string name { get; set; }
        public HideFlags hideFlags { get; set; }
        static bool Dead(Object o) { return ReferenceEquals(o, null) || o.destroyed; }
        public static bool operator ==(Object a, Object b) { if (Dead(a) || Dead(b)) return Dead(a) && Dead(b); return ReferenceEquals(a, b); }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object o) { return ReferenceEquals(this, o); }
        public override int GetHashCode() { return base.GetHashCode(); }
        public static implicit operator bool(Object o) { return o != null; }
        public static void Destroy(Object o) { if (!ReferenceEquals(o, null)) o.destroyed = true; }
        public static Object FindObjectOfType(Type t)
        {
            foreach (Object o in All) if (!o.destroyed && t.IsInstanceOfType(o)) return o;
            return null;
        }
        public static Object[] FindObjectsOfType(Type t)
        {
            var l = new List<Object>();
            foreach (Object o in All) if (!o.destroyed && t.IsInstanceOfType(o)) l.Add(o);
            return l.ToArray();
        }
        public override string ToString() { return name; }
    }

    public class Component : Object
    {
        internal GameObject go;
        public GameObject gameObject { get { return go; } }
        public Transform transform { get { return go != null ? go.transform : null; } }
        public T GetComponent<T>() where T : class { return go != null ? go.GetComponent<T>() : null; }
        public T[] GetComponents<T>() { return go != null ? go.GetComponents<T>() : new T[0]; }
    }
    public class Behaviour : Component { public bool enabled { get; set; } }
    public class MonoBehaviour : Behaviour
    {
        public MonoBehaviour() { enabled = true; useGUILayout = true; }
        public bool useGUILayout { get; set; }
    }

    public sealed class GameObject : Object
    {
        private readonly List<Component> _comps = new List<Component>();
        public readonly Transform transform;
        public GameObject(string n) { name = n; transform = new Transform(); transform.go = this; _comps.Add(transform); }
        public bool activeInHierarchy { get { return true; } }
        public static GameObject Find(string n)
        {
            foreach (Object o in All) { var g = o as GameObject; if (g != null && !g.destroyed && g.name == n) return g; }
            return null;
        }
        public T AddComponent<T>() where T : Component, new() { var c = new T(); c.go = this; _comps.Add(c); return c; }
        public static void Attach(GameObject g, Component c) { c.go = g; g._comps.Add(c); }
        public T GetComponent<T>() where T : class { foreach (var c in _comps) { var t = c as T; if (t != null) return t; } return null; }
        public T[] GetComponents<T>() { var l = new List<T>(); foreach (var c in _comps) if (c is T) l.Add((T)(object)c); return l.ToArray(); }
    }

    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Vector3 localScale { get; set; }
        public Transform() { localScale = new Vector3(1, 1, 1); }
        public Transform parent { get { return null; } }
        public int childCount { get { return 0; } }
        public Transform GetChild(int i) { return null; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public static Vector3 zero { get { return new Vector3(); } }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static bool operator ==(Vector3 a, Vector3 b) { return (a - b).magnitude < 1e-5f; }
        public static bool operator !=(Vector3 a, Vector3 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector3 && this == (Vector3)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode(); }
        public override string ToString() { return "(" + x + ", " + y + ", " + z + ")"; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public static Color clear { get { return new Color(0, 0, 0, 0); } }
    }
    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color32(Color c) { return new Color32(B(c.r), B(c.g), B(c.b), B(c.a)); }
        private static byte B(float v) { return (byte)Math.Round((v < 0 ? 0 : v > 1 ? 1 : v) * 255f); }
    }

    public class Texture : Object
    {
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }
    }
    public class Texture2D : Texture
    {
        public static int Created;
        public readonly int width, height;
        public Texture2D(int width, int height, TextureFormat textureFormat, bool mipChain) { this.width = width; this.height = height; Created++; }
        public void SetPixel(int x, int y, Color c) { }
        public void SetPixels(Color[] colors) { }
        public void SetPixels32(Color32[] colors) { if (colors.Length != width * height) throw new ArgumentException("SetPixels32: wrong size"); }
        public void Apply() { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
    }
    public enum TextureFormat { RGB24 = 3, RGBA32 = 4, ARGB32 = 5 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp }

    public enum FFTWindow { Rectangular = 0, BlackmanHarris = 5 }
    public sealed class AudioListener : Behaviour
    {
        public static void GetSpectrumData(float[] samples, int channel, FFTWindow window) { }
        public static void GetOutputData(float[] samples, int channel) { }
    }
    public sealed class AudioClip : Object { public float length { get { return 0; } } }
    public sealed class AudioSource : Behaviour { public bool isPlaying { get { return false; } } public AudioClip clip { get; set; } public bool loop { get; set; } }
    public sealed class Resources { public static Object[] FindObjectsOfTypeAll(Type type) { return Object.FindObjectsOfType(type); } }
    public sealed class Application { public static string unityVersion { get { return "2018.4.mock"; } } }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        internal static string Current = "core";
        public string name { get { return Current; } }
        public static void Load(string n) { Current = n; }
    }
    public class SceneManager { public static Scene GetActiveScene() { return new Scene(); } }
}
