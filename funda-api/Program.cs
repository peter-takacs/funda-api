using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace funda_api
{
    
    /**
     * I went with a console program, because the assignment did not mention any format, and setting up a fancy
     * frontend would take about as much time as writing this whole program, and would show nothing about my
     * .NET skills. I hope you like it.
     */
    class Program
    {
                
        private const string BaseUri =
            "http://partnerapi.funda.nl/feeds/Aanbod.svc/005e7c1d6f6c4f9bacac16760286e3cd/?type=koop&zo=/amsterdam";
        
        private const string TuinSuffix = "/tuin";
        
        /**
         * Straightforward as it gets. Give it a Uri, get an XML object.
         * We're not handling exceptions here, because I can't do anything helpful if the API is down.
         */
        private static async Task<XElement> ReadXmlFromUri(Uri uri)
        {
            using (var httpClient = new HttpClient())
            {
                var result = await httpClient.GetAsync(uri);
                var contentStream = await result.Content.ReadAsStreamAsync();
                return XElement.Load(contentStream);                    
            }           
        }

        /**
         * Since I haven't seen a way to request unpaginated data, we're looping through requests as long as there's
         * more pages.
         */
        private static async Task<IEnumerable<XElement>> ReadAllPages(string baseUri)
        {
            bool hasMorePages = true;
            int currentPage = 1;
            const int pageSize = 100;
            string ns = null;                        
            
            List<XElement> results = new List<XElement>();
            
            while (hasMorePages)
            {
                var locatieFeed = await ReadXmlFromUri(new Uri(baseUri + $"/&page={currentPage}&pagesize={pageSize}"));
                
                if (locatieFeed == null)
                {
                    return results;
                }
                
                if (ns == null)
                {
                    ns = locatieFeed.GetDefaultNamespace().NamespaceName;                    
                }

                var totalPages =
                    int.Parse(
                        locatieFeed
                            ?.Element(XName.Get("Paging", ns))
                            ?.Element(XName.Get("AantalPaginas", ns))?.Value ?? "0");
                currentPage++;
                hasMorePages = currentPage < totalPages;

                results.AddRange(locatieFeed.Descendants(XName.Get("Object", ns)));
                
                // This would be the place to do some throttling if we're afraid of saturating the API
            }

            return results;
        }

        
        private static void PrintMakelaarTable(IEnumerable<XElement> objects)
        {
            Console.WriteLine("{0, -40} {1, 10} {2, 10}", "Name", "Id", "Count");
            Console.WriteLine(string.Join("", Enumerable.Repeat("=", 62)));
            if (!(objects?.Any() ?? false))
            {
                return;
            }
            var ns = objects?.FirstOrDefault()?.GetDefaultNamespace()?.NamespaceName ?? "";
            
            /**
             * Here is where the magic happens. We don't seem to be dealing with massive amounts of data, so
             * LINQ is more than efficient enough. We group by MakelaarNaam and count the elements in the groups.
             */
            foreach (var entry in objects
                .GroupBy(e => e.Element(XName.Get("MakelaarNaam", ns)).Value)
                .OrderByDescending(m => m.Count())
                .Take(10))
            {
                if (!entry.Any())
                {
                    return;
                }
                Console.WriteLine("{0, -40} {1, 10} {2, 10}", 
                    entry.Key, entry.First().Element(XName.Get("MakelaarId", ns)).Value, entry.Count());
            }
        }
        
        static async Task Main(string[] args)
        {
            var amsterdamProperties = await ReadAllPages(BaseUri);
            PrintMakelaarTable(amsterdamProperties);
            var amsterdamPropertiesWithTuin = await ReadAllPages(BaseUri + TuinSuffix);
            Console.WriteLine();
            Console.WriteLine("With tuin in Amsterdam");
            PrintMakelaarTable(amsterdamPropertiesWithTuin);
            Console.ReadLine();
        }
    }
}