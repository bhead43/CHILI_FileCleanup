using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Text;

void Main()
{
    List<string> configs = File.ReadLines("config.txt").ToList();
    string filePath = configs[0].Split('=')[1];
    DateTime dateToDelete = DateTime.Parse(configs[1].Split('=')[1]);
    string resourceType = configs[2].Split('=')[1];

    //Set this up to take either Documents or Assets later
    string privateDataPath = filePath + @"\Private_Data\" + resourceType;
    string resourcesPath = filePath + @"\Resources\" + resourceType;
    string cacheDataPath = filePath + @"\Cache_Data\" + resourceType;

    List<string> ids = new List<string>();
    List<XElement> itemsToKeep = new List<XElement>();

    //Read fileInfos from Private_Data/{resourceType}
    string[] privateDataDirs = Directory.GetDirectories(privateDataPath);
    ids = privateDataDirs.Where(filePath => DateTime.Parse(GetXMLAttribute((filePath + @"\fileInfo.xml"), "fileIndexed")) < dateToDelete)
        .Select(filePath => new DirectoryInfo(filePath).Name).ToList<string>();

    Console.WriteLine(ids.Count() + " items to be deleted");

    //Read each data.xml, navigate to and delete files if id exists in list
    //  -If id isn't in list, save the current element to itemsToKeep
    string[] dataXMLPaths = Directory.GetFiles(resourcesPath, "data*.xml");

    foreach (var file in dataXMLPaths)
    {
        XElement dataXML = XElement.Parse(File.ReadAllText(file));
        List<XElement> items = dataXML.Element("items").Elements().ToList<XElement>();
        foreach (var item in items)
        {
            if (ids.Contains(item.Attribute("id").Value))
            {
                //Delete Resources, Private_Data, and (if it exists) Cache_Data entries
                List<DirectoryInfo> dirs = new List<DirectoryInfo>()
                {
                    new DirectoryInfo(resourcesPath + @"\" + item.Attribute("relativePath").Value),
                    new DirectoryInfo(privateDataPath + @"\" + item.Attribute("id").Value),
                    new DirectoryInfo(cacheDataPath + @"\" + item.Attribute("id").Value)
                };

                foreach (var dir in dirs)
                {
                    if (dir.Exists)
                    {
                        dir.Delete(true);
                    }
                }
            }
            else
            {
                itemsToKeep.Add(item);
            }
        }
        //Delete the data.xml
        File.Delete(file);
    }

    //Rewrite dataXMLs, stuffing 1000 items per document
    int dataCount = 0;
    int batchSize = 1000;
    var batches = itemsToKeep
        .Select((item, index) => new { item, index })
        .GroupBy(_ => _.index / batchSize, _ => _.item);

    foreach (var batch in batches)
    {
        dataCount++;
        WriteDataXML(dataCount, batch.ToList(), resourcesPath);
    }

}

//Read and parse an XML file, and return an attribute value
string GetXMLAttribute(string xmlPath, string attributeName)
{
    try
    {
        string xmlString = File.ReadAllText(xmlPath);
        XElement xml = XElement.Parse(xmlString);

        return xml.Attribute(attributeName).Value;
    }
    //if file at xmlPath doesn't exist
    catch (System.IO.FileNotFoundException)
    {
        return DateTime.Today.ToShortDateString();
    }
}

//Writes a new data.xml file from itemsToKeep
async void WriteDataXML(int count, List<XElement> items, string basePath)
{
    string fileName;
    if (count == 1)
    {
        fileName = "data.xml";
    }
    else
    {
        fileName = "data" + count.ToString("D7") + ".xml";
    }

    //batch items into groups of... like 50?
    int batchSize = 50;
    var batches = items
        .Select((item, index) => new { item, index })
        .GroupBy(_ => _.index / batchSize, _ => _.item);
    //*Might* help the issue of dataxmls being cutoff mid write every now and then
    using (StreamWriter file = new StreamWriter(basePath + @"\" + fileName, append: true, Encoding.UTF8, 200000))
    {
        await file.WriteAsync(@"<Documents><items>");
    }
    foreach (var batch in batches)
    {
        using (StreamWriter file = new StreamWriter(basePath + @"\" + fileName, append: true, Encoding.UTF8, 200000))
        {
            foreach (var item in batch.ToList())
            {
                await file.WriteAsync(item.ToString());
            }
        }
    }
    using (StreamWriter file = new StreamWriter(basePath + @"\" + fileName, append: true, Encoding.UTF8, 200000))
    {
        await file.WriteAsync(@"</items></Documents>");
    }
}

Main();