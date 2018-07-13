using System;
using System.IO;
using System.Text;

namespace StoredProcedureMapping
{
    class Program
    {
        static void Main(string[] args)
        {
            var sb = new StringBuilder();
            new StoredProcedureMapper(@"D:\SysDev_Current\Retail\CoreData\Zebra\Live\Stored Procedures", s => sb.AppendLine(s))
                .Map("dbo", "GenerateInvoices");

            Console.WriteLine(sb.ToString());
            File.WriteAllText("out.txt", sb.ToString());
        }
    }
}
