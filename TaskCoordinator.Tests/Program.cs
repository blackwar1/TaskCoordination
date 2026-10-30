using System.Diagnostics;
using TaskCoordinator;

var pass = 0;
var fail = 0;
void Check(bool ok, string n)
{
    if (ok) { pass++; Console.WriteLine("PASS " + n); }
    else { fail++; Console.WriteLine("FAIL " + n); }
}

var s = new DiagnosticService();

// базовые из шаблона
var one = await s.RunAsync("Сеть", TimeSpan.FromMilliseconds(20));
Check(one.Status == DiagnosticStatus.Completed, "одна диагностика завершается");

var all = await s.RunAllAsync();
Check(all.Count == 3 && all.All(x => x.Status == DiagnosticStatus.Completed),
      "WhenAll ждёт три диагностики");

var first = await s.FirstFinishedAsync();
Check(first == "Быстрая", "WhenAny возвращает первую завершившуюся");

// --- новые тесты ---

// Кооперативная отмена: статус Cancelled, а не исключение
using (var cts = new CancellationTokenSource())
{
    var t = s.RunAsync("Долгая", TimeSpan.FromSeconds(5), cts.Token);
    cts.CancelAfter(100);
    var r = await t;
    Check(r.Status == DiagnosticStatus.Cancelled, "отмена -> Cancelled");
}

// Тайм-аут через linked CTS -> TimeoutException
try
{
    await s.RunWithTimeoutAsync("Тайм-аут", TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(150));
    Check(false, "тайм-аут -> TimeoutException");
}
catch (TimeoutException)
{
    Check(true, "тайм-аут -> TimeoutException");
}

// Внешняя отмена через RunWithTimeoutAsync -> OperationCanceledException, не TimeoutException
using (var cts = new CancellationTokenSource())
{
    var t = s.RunWithTimeoutAsync("Внешняя отмена", TimeSpan.FromSeconds(5),
                                  TimeSpan.FromSeconds(10), cts.Token);
    cts.CancelAfter(100);
    try
    {
        await t;
        Check(false, "внешняя отмена -> OperationCanceledException");
    }
    catch (TimeoutException)
    {
        Check(false, "внешняя отмена -> OperationCanceledException (пришёл TimeoutException)");
    }
    catch (OperationCanceledException)
    {
        Check(true, "внешняя отмена -> OperationCanceledException");
    }
}

// Неверное имя -> ArgumentException
try
{
    await s.RunAsync("   ", TimeSpan.FromMilliseconds(1));
    Check(false, "пустое имя -> ArgumentException");
}
catch (ArgumentException) { Check(true, "пустое имя -> ArgumentException"); }

// Отрицательная длительность -> ArgumentOutOfRangeException
try
{
    await s.RunAsync("Диск", TimeSpan.FromMilliseconds(-1));
    Check(false, "отрицательное время -> ArgumentOutOfRangeException");
}
catch (ArgumentOutOfRangeException) { Check(true, "отрицательное время -> ArgumentOutOfRangeException"); }

// CPU-bound отмена
using (var cts = new CancellationTokenSource())
{
    var t = s.RunCpuBoundAsync("CPU", 100_000_000, cts.Token);
    cts.CancelAfter(50);
    var r = await t;
    Check(r.Status == DiagnosticStatus.Cancelled, "CPU-bound отмена -> Cancelled");
}

// RunSafeAsync: ошибка превращается в Failed, не теряется
var failed = await s.RunSafeAsync("", TimeSpan.FromMilliseconds(1));
Check(failed.Status == DiagnosticStatus.Failed && failed.Details.Contains("ArgumentException"),
      "RunSafeAsync -> Failed с текстом ошибки");

Console.WriteLine($"Итого: {pass} пройдено, {fail} ошибок.");
return fail == 0 ? 0 : 1;