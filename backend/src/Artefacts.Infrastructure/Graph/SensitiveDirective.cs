using System.Reflection;
using Artefacts.Infrastructure.Read;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;

namespace Artefacts.Infrastructure.Graph;

/// <summary>
/// Tags a GraphQL field that exposes sensitive data (for instance <c>financial</c>). Metadata only for now: when roles
/// arrive, one field middleware enforces it.
/// </summary>
public sealed record SensitiveDirective(string Tag);

public sealed class SensitiveDirectiveType : DirectiveType<SensitiveDirective>
{
    protected override void Configure(IDirectiveTypeDescriptor<SensitiveDirective> descriptor)
    {
        descriptor.Name("sensitive");
        descriptor.Description("The field exposes sensitive data of this kind. Informational until access is restricted by role.");
        descriptor.Location(DirectiveLocation.FieldDefinition);
    }
}

/// <summary>
/// Applies <c>@sensitive</c> to a field, with the tag the artefact read view gives to the named property
/// (<see cref="Read.SensitiveAttribute"/>), so the schema and the field catalog cannot disagree.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class SensitiveFieldAttribute(string artefactRowProperty) : ObjectFieldDescriptorAttribute
{
    protected override void OnConfigure(IDescriptorContext context, IObjectFieldDescriptor descriptor, MemberInfo? member)
    {
        var property = typeof(ArtefactRow).GetProperty(artefactRowProperty)
                       ?? throw new InvalidOperationException($"ArtefactRow has no property '{artefactRowProperty}'.");
        var tag = property.GetCustomAttribute<Read.SensitiveAttribute>()?.Classification
                  ?? throw new InvalidOperationException($"ArtefactRow.{artefactRowProperty} is not marked sensitive.");
        descriptor.Directive(new SensitiveDirective(tag));
    }
}
