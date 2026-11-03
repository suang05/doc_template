using System.Collections.Frozen;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Infrastructure.Engines;

public sealed class RenderEngineFactory(IEnumerable<IRenderEngine> engines) : IRenderEngineFactory
{
    private readonly FrozenDictionary<RenderEngineType, IRenderEngine> _engines = engines.ToFrozenDictionary(e => e.EngineType);

    public IRenderEngine GetEngine(RenderEngineType engineType)
    {
        if (_engines.TryGetValue(engineType, out var engine))
        {
            return engine;
        }

        throw new RenderException(
            templateSlug: "system",
            engineType: engineType.ToString(),
            reason: $"No render engine registered for engine type '{engineType}'.");
    }
}

