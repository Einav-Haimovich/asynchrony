using LifeBeforeAsync;

Console.WriteLine("Cooking has started!");
var turkey = new Turkey();
var gravy = new Gravy();


await Task.WhenAll(turkey.Cook(), gravy.Cook());

Console.WriteLine("Cooking is complete!");