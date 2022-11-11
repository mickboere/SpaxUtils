using UnityEngine;

/// <summary>
/// Display a field as read-only in the inspector.
/// CustomPropertyDrawers will not work when this attribute is used.
/// </summary>
/// <seealso cref="BeginReadOnlyGroupAttribute"/>
/// <seealso cref="EndReadOnlyGroupAttribute"/>
public class ReadOnlyAttribute : PropertyAttribute
{

}
