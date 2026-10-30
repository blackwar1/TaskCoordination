using System.Diagnostics;

namespace TaskCoordinator;

public sealed class DiagnosticService
{
    // TODO 1: валидация имени и длительности
    private static void Validate(string name, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Имя диагностики не может быть пустым.", nameof(name));
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Длительность не может быть отрицательной.");
    }

    public async Task<DiagnosticResult> RunAsync(string name, TimeSpan duration, CancellationToken token = default)
    {
        Validate(name, duration);

        var sw = Stopwatch.StartNew();
        try
        {
            // TODO 2: передаём токен в Task.Delay — отмена кооперативная
            await Task.Delay(duration, token);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            // Возвращаем Cancelled, а не пробрасываем — вызывающий сам решит,
            // как трактовать (см. RunWithTimeoutAsync / FirstFinishedAsync).
            return new DiagnosticResult(name, DiagnosticStatus.Cancelled, "Отменено", sw.Elapsed);
        }
        sw.Stop();

        // TODO 3: Completed + замеренное время
        return new DiagnosticResult(
            name,
            DiagnosticStatus.Completed,
            $"Выполнено за {sw.ElapsedMilliseconds} мс",
            sw.Elapsed);
    }

    public async Task<DiagnosticResult> RunCpuBoundAsync(string name, int iterations, CancellationToken token = default)
    {
        if (iterations < 0)
            throw new ArgumentOutOfRangeException(nameof(iterations));

        // TODO 4: CPU-bound цикл — в Task.Run, не Task.Delay
        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                long sum = 0;
                for (int i = 0; i < iterations; i++)
                {
                    // Проверка токена — регулярно, каждые 1000 итераций
                    if (i % 1000 == 0) token.ThrowIfCancellationRequested();
                    sum += i % 7;
                }
                sw.Stop();
                return new DiagnosticResult(
                    name,
                    DiagnosticStatus.Completed,
                    $"Сумма: {sum}",
                    sw.Elapsed);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return new DiagnosticResult(name, DiagnosticStatus.Cancelled, "Отменено", sw.Elapsed);
            }
        }, token);
    }

    public async Task<IReadOnlyList<DiagnosticResult>> RunAllAsync(CancellationToken token = default)
    {
        // TODO 5: три независимые диагностики запускаются одновременно
        var tasks = new[]
        {
            RunAsync("Сеть",  TimeSpan.FromMilliseconds(600), token),
            RunAsync("Диск",  TimeSpan.FromMilliseconds(900), token),
            RunAsync("База",  TimeSpan.FromMilliseconds(700), token)
        };

        // WhenAll ждёт всех; каждый результат уже несёт свой статус
        var results = await Task.WhenAll(tasks);
        return results;
    }

    public async Task<DiagnosticResult> RunWithTimeoutAsync(
        string name, TimeSpan duration, TimeSpan timeout, CancellationToken token = default)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        // TODO 6: linked CTS — отмена придёт либо от внешнего token, либо от тайм-аута
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);

        // TODO 7: CancelAfter
        linked.CancelAfter(timeout);

        try
        {
            return await RunAsync(name, duration, linked.Token);
        }
        catch (OperationCanceledException)
        {
            // TODO 8: различаем тайм-аут и внешнюю отмену
            if (token.IsCancellationRequested)
            {
                // внешний вызывающий отменил — пробрасываем как обычно
                throw;
            }

            if (linked.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Диагностика '{name}' не уложилась в {timeout.TotalMilliseconds} мс.");
            }

            throw; // страховка
        }
    }

    public async Task<string> FirstFinishedAsync(CancellationToken token = default)
    {
        var tasks = new[]
        {
            RunAsync("Быстрая",  TimeSpan.FromMilliseconds(150), token),
            RunAsync("Средняя",  TimeSpan.FromMilliseconds(350), token),
            RunAsync("Медленная", TimeSpan.FromMilliseconds(600), token)
        };

        // TODO 9: WhenAny — ждём первую завершившуюся
        var first = await Task.WhenAny(tasks);
        var result = await first;      // пробрасываем её возможные ошибки
        return result.Name;
    }

    // TODO 10: обёртка, превращающая исключения в статусы Failed/Cancelled
    public async Task<DiagnosticResult> RunSafeAsync(
        string name, TimeSpan duration, CancellationToken token = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await RunAsync(name, duration, token);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new DiagnosticResult(name, DiagnosticStatus.Cancelled, "Отменено", sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            // Не пустой catch — сохраняем тип и текст ошибки в Details
            return new DiagnosticResult(
                name,
                DiagnosticStatus.Failed,
                $"{ex.GetType().Name}: {ex.Message}",
                sw.Elapsed);
        }
    }
}