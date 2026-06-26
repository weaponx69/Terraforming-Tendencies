// ============================================================
// VisualScriptingAttributes.cs  (Runtime Assembly)
//
// Compilation stub for the Unity Visual Scripting pipeline.
// Defines empty attribute stand-ins inside the real
// Unity.VisualScripting namespace so every runtime script
// compiles cleanly whether or not com.unity.visualscripting
// is installed.
//
// Resolution order:
//   VS installed   → Unity.VisualScripting package assembly
//                    wins; this file's #if block is skipped.
//   VS not installed → this file provides the stubs so
//                    [IncludeInSettings] and [Inspectable]
//                    resolve without error.
//
// Location: Assets/Scripts/  (MainGame runtime assembly)
// DO NOT move into Assets/Scripts/Editor/ — Editor assemblies
// are invisible to runtime scripts.
// ============================================================

#if !UNITY_VISUALSCRIPTING_1_7_OR_NEWER

namespace Unity.VisualScripting
{
    using System;

    /// <summary>
    /// Stub: marks a type for inclusion in the Visual Scripting
    /// Type Options registry.
    /// Mirrors <c>Unity.VisualScripting.IncludeInSettingsAttribute</c>.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class  |
        AttributeTargets.Struct |
        AttributeTargets.Enum   |
        AttributeTargets.Interface,
        Inherited    = false,
        AllowMultiple = false)]
    public sealed class IncludeInSettingsAttribute : Attribute
    {
        /// <summary>When <c>true</c> the type is included; <c>false</c> excludes it.</summary>
        public bool include { get; }

        public IncludeInSettingsAttribute(bool include = true)
        {
            this.include = include;
        }
    }

    /// <summary>
    /// Stub: exposes a field, property, or method to the Visual
    /// Scripting graph inspector panel.
    /// Mirrors <c>Unity.VisualScripting.InspectableAttribute</c>.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field    |
        AttributeTargets.Property |
        AttributeTargets.Method,
        Inherited     = true,
        AllowMultiple = false)]
    public sealed class InspectableAttribute : Attribute
    {
        public InspectableAttribute() { }
    }
}

#endif // !UNITY_VISUALSCRIPTING_1_7_OR_NEWER
