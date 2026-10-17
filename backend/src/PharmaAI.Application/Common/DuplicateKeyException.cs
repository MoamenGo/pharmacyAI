namespace PharmaAI.Application.Common;

/// <summary>A save hit a unique index: another writer created the same row first.</summary>
public sealed class DuplicateKeyException(Exception inner)
    : Exception("A record with the same unique key already exists.", inner);
