using System.Collections.Generic;
using System.Linq;

namespace ServerMonitor.Graphics;

// 用于存储和管理历史数据点的辅助类 (环形缓冲区)
public class HistoryData
{
    private readonly Queue<double> _queue;
    private readonly int _capacity;

    public HistoryData(int capacity = 100)
    {
        _capacity = capacity;
        _queue = new Queue<double>(capacity);
        // 预填充0，避免刚启动时线条长度不足
        for (int i = 0; i < capacity; i++) _queue.Enqueue(0);
    }

    public void Add(double value)
    {
        if (_queue.Count >= _capacity) _queue.Dequeue();
        _queue.Enqueue(value);
    }

    public double[] ToArray() => _queue.ToArray();
    public double Max() => _queue.Count > 0 ? _queue.Max() : 0;
}