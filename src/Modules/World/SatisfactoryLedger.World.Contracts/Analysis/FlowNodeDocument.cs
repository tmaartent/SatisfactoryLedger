using System.Text.Json.Serialization;

namespace SatisfactoryLedger.World.Contracts.Analysis;

/// <summary>One node of a flow network. <c>kind</c> in the JSON says which (System.Text.Json polymorphism).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ProviderDocument), "provider")]
[JsonDerivedType(typeof(UnknownSourceDocument), "unknownSource")]
[JsonDerivedType(typeof(MachineDocument), "machine")]
[JsonDerivedType(typeof(ConduitDocument), "conduit")]
[JsonDerivedType(typeof(SplitterDocument), "splitter")]
[JsonDerivedType(typeof(MergerDocument), "merger")]
[JsonDerivedType(typeof(SinkDocument), "sink")]
public abstract record FlowNodeDocument(string Id);
