namespace AetherXIV.Map;

public sealed class MapScriptEventCommandBuffer : IMapScriptEventOutbox
{
    private readonly AsyncLocal<Capture?> activeCapture = new();

    public Capture BeginCapture()
    {
        Capture capture = new(this, activeCapture.Value);
        activeCapture.Value = capture;
        return capture;
    }

    public ValueTask EnqueueAsync(MapScriptOutboxItem item, CancellationToken cancellationToken = default)
    {
        Capture capture = activeCapture.Value
            ?? throw new InvalidOperationException("Map script event commands must be emitted during an active Map event dispatch.");
        capture.Add(item);

        return ValueTask.CompletedTask;
    }

    public sealed class Capture : IDisposable
    {
        private readonly MapScriptEventCommandBuffer owner;
        private readonly Capture? previous;
        private readonly List<MapScriptOutboxItem> items = [];
        private bool disposed;

        internal Capture(MapScriptEventCommandBuffer owner, Capture? previous)
        {
            this.owner = owner;
            this.previous = previous;
        }

        internal void Add(MapScriptOutboxItem item)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            items.Add(item);
        }

        public IReadOnlyList<MapScriptOutboxItem> Drain()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (items.Count == 0)
                return [];

            MapScriptOutboxItem[] drained = items.ToArray();
            items.Clear();
            return drained;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (!ReferenceEquals(owner.activeCapture.Value, this))
                throw new InvalidOperationException("Map script event command captures must be disposed in stack order.");

            owner.activeCapture.Value = previous;
            disposed = true;
        }
    }
}
