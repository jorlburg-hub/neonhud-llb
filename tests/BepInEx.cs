using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BepInPlugin : Attribute
    {
        public BepInPlugin(string GUID, string Name, string Version) { this.GUID = GUID; this.Name = Name; this.Version = new Version(Version); }
        public string GUID { get; protected set; }
        public string Name { get; protected set; }
        public Version Version { get; protected set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BepInDependency : Attribute
    {
        [Flags] public enum DependencyFlags { HardDependency = 1, SoftDependency = 2 }
        public BepInDependency(string DependencyGUID, DependencyFlags Flags = DependencyFlags.HardDependency) { this.DependencyGUID = DependencyGUID; this.Flags = Flags; }
        public string DependencyGUID { get; protected set; }
        public DependencyFlags Flags { get; protected set; }
    }

    public class PluginInfo
    {
        public BepInPlugin Metadata { get; set; }
        public BaseUnityPlugin Instance { get; set; }
        public string Location { get; set; }
        public override string ToString() { return Metadata.Name + " " + Metadata.Version; }
    }

    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        private readonly Logging.ManualLogSource _log = new Logging.ManualLogSource();
        private readonly Configuration.ConfigFile _cfg = new Configuration.ConfigFile();
        protected Logging.ManualLogSource Logger { get { return _log; } }
        public Configuration.ConfigFile Config { get { return _cfg; } }
        public PluginInfo Info { get; private set; }

        protected BaseUnityPlugin()
        {
            var meta = (BepInPlugin)Attribute.GetCustomAttribute(GetType(), typeof(BepInPlugin));
            Info = new PluginInfo { Metadata = meta, Instance = this };
            GameObject.Attach(new GameObject("BepInEx_Manager"), this);
        }
    }

    public static class Paths
    {
        public static string Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bepinex_mock");
        static Paths() { System.IO.Directory.CreateDirectory(Root); }
        public static string BepInExRootPath { get { return Root; } }
        public static string CachePath { get { return System.IO.Path.Combine(Root, "cache"); } }
        public static string ConfigPath { get { return System.IO.Path.Combine(Root, "config"); } }
    }
}

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public static readonly List<string> Lines = new List<string>();
        public void LogInfo(object o) { Lines.Add("[info] " + o); Console.WriteLine("    [info] " + o); }
        public void LogWarning(object o) { Lines.Add("[warn] " + o); Console.WriteLine("    [warn] " + o); }
        public void LogError(object o) { Lines.Add("[err] " + o); Console.WriteLine("    [err] " + o); }
        public void LogDebug(object o) { }
    }
}

namespace BepInEx.Configuration
{
    public abstract class AcceptableValueBase
    {
        protected AcceptableValueBase(Type valueType) { ValueType = valueType; }
        public Type ValueType { get; private set; }
        public abstract object Clamp(object value);
        public abstract bool IsValid(object value);
    }

    public class AcceptableValueRange<T> : AcceptableValueBase where T : IComparable
    {
        public AcceptableValueRange(T minValue, T maxValue) : base(typeof(T)) { MinValue = minValue; MaxValue = maxValue; }
        public virtual T MinValue { get; private set; }
        public virtual T MaxValue { get; private set; }
        public override object Clamp(object value)
        {
            if (MinValue.CompareTo(value) > 0) return MinValue;
            if (MaxValue.CompareTo(value) < 0) return MaxValue;
            return value;
        }
        public override bool IsValid(object value) { return MinValue.CompareTo(value) <= 0 && MaxValue.CompareTo(value) >= 0; }
    }

    public class AcceptableValueList<T> : AcceptableValueBase where T : IEquatable<T>
    {
        public AcceptableValueList(params T[] acceptableValues) : base(typeof(T)) { AcceptableValues = acceptableValues; }
        public virtual T[] AcceptableValues { get; private set; }
        public override object Clamp(object value) { return IsValid(value) ? value : AcceptableValues[0]; }
        public override bool IsValid(object value) { return value is T && Array.Exists(AcceptableValues, x => x.Equals((T)value)); }
    }

    public class ConfigDescription
    {
        public ConfigDescription(string description, AcceptableValueBase acceptableValues = null, params object[] tags)
        {
            Description = description; AcceptableValues = acceptableValues; Tags = tags ?? new object[0];
        }
        public string Description { get; private set; }
        public AcceptableValueBase AcceptableValues { get; private set; }
        public object[] Tags { get; private set; }
    }

    public class ConfigDefinition
    {
        public ConfigDefinition(string section, string key) { Section = section; Key = key; }
        public string Section { get; private set; }
        public string Key { get; private set; }
        public override string ToString() { return Section + "." + Key; }
    }

    public sealed class SettingChangedEventArgs : EventArgs
    {
        public SettingChangedEventArgs(ConfigEntryBase changedSetting) { ChangedSetting = changedSetting; }
        public ConfigEntryBase ChangedSetting { get; private set; }
    }

    public abstract class ConfigEntryBase
    {
        public ConfigFile ConfigFile { get; internal set; }
        public ConfigDefinition Definition { get; internal set; }
        public ConfigDescription Description { get; internal set; }
        public abstract Type SettingType { get; }
        public object DefaultValue { get; internal set; }
        public abstract object BoxedValue { get; set; }
    }

    public sealed class ConfigEntry<T> : ConfigEntryBase
    {
        private T _v;
        public event EventHandler SettingChanged;
        public override Type SettingType { get { return typeof(T); } }
        public T Value
        {
            get { return _v; }
            set
            {
                if (Description != null && Description.AcceptableValues != null) value = (T)Description.AcceptableValues.Clamp(value);
                if (Equals(_v, value)) return;
                _v = value;
                if (SettingChanged != null) SettingChanged(this, EventArgs.Empty);
                if (ConfigFile != null) ConfigFile.Changed(this);
            }
        }
        internal void Init(T v) { _v = v; }
        public override object BoxedValue { get { return Value; } set { Value = (T)value; } }
    }

    public struct KeyboardShortcut
    {
        private readonly KeyCode[] _keys;
        public KeyboardShortcut(KeyCode mainKey, params KeyCode[] modifiers)
        {
            _keys = new KeyCode[1 + (modifiers != null ? modifiers.Length : 0)];
            _keys[0] = mainKey;
            if (modifiers != null) Array.Copy(modifiers, 0, _keys, 1, modifiers.Length);
        }
        public static readonly KeyboardShortcut Empty = new KeyboardShortcut();
        public KeyCode MainKey { get { return _keys != null && _keys.Length > 0 ? _keys[0] : KeyCode.None; } }
        public IEnumerable<KeyCode> Modifiers { get { var l = new List<KeyCode>(); if (_keys != null) for (int i = 1; i < _keys.Length; i++) l.Add(_keys[i]); return l; } }
        private static readonly KeyCode[] Block = { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt };
        private bool ModifiersOk()
        {
            foreach (KeyCode k in Modifiers) if (!Input.GetKey(k)) return false;
            foreach (KeyCode b in Block) if (Input.GetKey(b) && Array.IndexOf(_keys, b) < 0) return false;
            return true;
        }
        public bool IsDown() { return MainKey != KeyCode.None && Input.GetKeyDown(MainKey) && ModifiersOk(); }
        public bool IsPressed() { return MainKey != KeyCode.None && Input.GetKey(MainKey) && ModifiersOk(); }
        public override string ToString()
        {
            if (MainKey == KeyCode.None) return "Not set";
            var parts = new List<string>();
            foreach (KeyCode k in _keys) parts.Add(k.ToString());
            return string.Join(" + ", parts.ToArray());
        }
        public override bool Equals(object obj)
        {
            if (!(obj is KeyboardShortcut)) return false;
            var o = (KeyboardShortcut)obj;
            if (MainKey != o.MainKey) return false;
            var a = new List<KeyCode>(Modifiers); var b = new List<KeyCode>(o.Modifiers);
            if (a.Count != b.Count) return false;
            foreach (KeyCode k in a) if (!b.Contains(k)) return false;
            return true;
        }
        public override int GetHashCode() { return (int)MainKey; }
    }

    public class ConfigFile
    {
        public static readonly Dictionary<string, object> Saved = new Dictionary<string, object>();
        private readonly List<ConfigEntryBase> _entries = new List<ConfigEntryBase>();
        public bool SaveOnConfigSet { get; set; }
        public int Saves, Reloads;
        public event EventHandler<SettingChangedEventArgs> SettingChanged;

        public ConfigFile() { SaveOnConfigSet = true; }

        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            return Bind(section, key, defaultValue, new ConfigDescription(description, null));
        }

        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription configDescription = null)
        {
            foreach (ConfigEntryBase old in _entries)
                if (old.Definition.Section == section && old.Definition.Key == key) throw new ArgumentException("bound twice: " + section + "." + key);
            var e = new ConfigEntry<T> { ConfigFile = this, Definition = new ConfigDefinition(section, key), Description = configDescription, DefaultValue = defaultValue };
            object saved;
            T v = Saved.TryGetValue(section + "." + key, out saved) ? (T)saved : defaultValue;
            if (configDescription != null && configDescription.AcceptableValues != null) v = (T)configDescription.AcceptableValues.Clamp(v);
            e.Init(v);
            _entries.Add(e);
            return e;
        }

        public IEnumerable<ConfigEntryBase> Entries { get { return _entries; } }
        public int Count { get { return _entries.Count; } }

        internal void Changed(ConfigEntryBase entry)
        {
            if (SaveOnConfigSet) Save();
            if (SettingChanged != null) SettingChanged(this, new SettingChangedEventArgs(entry));
        }

        public void Save() { Saves++; }
        public void Reload() { Reloads++; }
    }
}
