// ============================================================
// VisualScriptingAttributes.cs  (Runtime Assembly  –  Permanent stub)
//
// Provides [IncludeInSettings] and [Inspectable] attribute
// definitions so every runtime script in MainGame compiles
// cleanly.
//
// WHY THIS FILE EXISTS:
//   Unity.VisualScripting.Core is the assembly that owns these
//   attributes in the official VS package.  Adding it to
//   MainGame.asmdef.references brings in the real types, but
//   Unity's linker does not expose Core transitively through
//   the Flow / State references.  This stub guarantees
//   forward-compatibility: if Core is not resolvable for any
//   reason, this file is the fallback.
//
// REMOVAL:
//   Delete this file only after confirming that
//   Unity.VisualScripting.Core is listed in MainGame.asmdef
//   AND that a project-wide recompile shows zero CS0246 errors.
//
// Location: Assets/Scripts/  (MainGame runtime assembly)
// DO NOT move into Assets/Scripts/Editor/ — Editor assemblies
// are invisible to runtime scripts.
// ============================================================

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
        Inherited     = false,
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
