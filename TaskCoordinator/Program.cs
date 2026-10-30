using TaskCoordinator;
var service = new DiagnosticService();
while (true)
{
    Console.WriteLine("\n1 — три диагностики; 2 — отмена; 3 — тайм-аут; 4 — WhenAny; 0 — выход");
    var cmd = Console.ReadLine();
    if (cmd == "0")
        break;
    try
    {
        if (cmd == "1")
        {
            foreach (var x in await service.RunAllAsync())
                Console.WriteLine($"{x.Name}: {x.Status} {x.Details}");
        }
        else if (cmd == "2")
        {
            using var cts = new CancellationTokenSource();
            var task = service.RunAsync("Долгая", TimeSpan.FromSeconds(5), cts.Token);
            await Task.Delay(400);
            cts.Cancel();
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Отменено пользователем.");
            }
        }
        else if (cmd == "3")
        {
            try
            {
                Console.WriteLine(
                    await service.RunWithTimeoutAsync(
                        "Тайм-аут",
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(1)
                    )
                );
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        else if (cmd == "4")
            Console.WriteLine("Первой завершилась: " + await service.FirstFinishedAsync());
        else
            Console.WriteLine("Неизвестная команда.");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Отменено.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка {ex.GetType().Name}: {ex.Message}");
    }
}
