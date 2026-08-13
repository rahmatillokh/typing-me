using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TypingMe.EditorTools
{
    /// <summary>
    /// Sets [SerializeField] private fields from editor code.
    /// </summary>
    /// <remarks>
    /// Deliberately throws when a property path doesn't exist: the bootstrap wires dozens of references,
    /// and a silently-skipped rename would surface much later as a null at runtime.
    /// </remarks>
    internal sealed class Wiring : IDisposable
    {
        private readonly SerializedObject _serialized;

        public Wiring(Object target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            _serialized = new SerializedObject(target);
        }

        private SerializedProperty Prop(string path)
        {
            SerializedProperty property = _serialized.FindProperty(path);
            if (property == null)
                throw new InvalidOperationException(
                    $"'{path}' not found on {_serialized.targetObject.GetType().Name}. Field renamed?");

            return property;
        }

        public Wiring Ref(string path, Object value)
        {
            Prop(path).objectReferenceValue = value;
            return this;
        }

        public Wiring Float(string path, float value)
        {
            Prop(path).floatValue = value;
            return this;
        }

        public Wiring Int(string path, int value)
        {
            Prop(path).intValue = value;
            return this;
        }

        public Wiring Bool(string path, bool value)
        {
            Prop(path).boolValue = value;
            return this;
        }

        public Wiring Str(string path, string value)
        {
            Prop(path).stringValue = value;
            return this;
        }

        public Wiring Color(string path, Color value)
        {
            Prop(path).colorValue = value;
            return this;
        }

        public Wiring Enum(string path, int value)
        {
            Prop(path).enumValueIndex = value;
            return this;
        }

        public Wiring Curve(string path, AnimationCurve value)
        {
            Prop(path).animationCurveValue = value;
            return this;
        }

        public Wiring Vector2(string path, Vector2 value)
        {
            Prop(path).vector2Value = value;
            return this;
        }

        public Wiring RefArray(string path, params Object[] values)
        {
            SerializedProperty property = Prop(path);
            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            return this;
        }

        public Wiring FloatArray(string path, params float[] values)
        {
            SerializedProperty property = Prop(path);
            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).floatValue = values[i];

            return this;
        }

        /// <summary>Grows a list of serializable structs and hands back each element to be filled in.</summary>
        public Wiring Elements(string path, int count, Action<SerializedProperty, int> fill)
        {
            SerializedProperty property = Prop(path);
            property.arraySize = count;

            for (int i = 0; i < count; i++)
                fill(property.GetArrayElementAtIndex(i), i);

            return this;
        }

        public void Dispose() => _serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
