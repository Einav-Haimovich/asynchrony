using CreatingTaskFromScratch;

Console.WriteLine($"Current Tread Id: {Thread.CurrentThread.ManagedThreadId}");


MyTask.Run(() => Console.WriteLine($"First Thread Id: {Environment.CurrentManagedThreadId}")).Wait();

MyTask.Delay(TimeSpan.FromSeconds(3)).Wait();

MyTask.Run(() => Console.WriteLine($"Second Thread Id: {Environment.CurrentManagedThreadId}")).Wait();

Console.ReadLine();