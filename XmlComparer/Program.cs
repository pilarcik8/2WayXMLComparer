using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

var addedElementCounts = new List<int>();
var missingElementCounts = new List<int>();
var wrongPositionCounts = new List<int>();
var wrongValueCounts = new List<int>();
var indexesWithMistakes = new List<int>();

int index = 0;

int countNotValidXMLFiles = 0;
int countTotalFiles = 0;
int countCorrectFiles = 0;


bool ordersMatters = UserAnswerOrderMatters();
Console.WriteLine("Vložte absolútnu cestu k priečinkami (pomenované 0, 1, 2...) obshahujúce súbor expected{iterácia}.xml");
string pathGeneratedXMLDir = UserInputDirToFiles();
Console.WriteLine("Vložte absolútnu cestu k priečinku so generovanými mergovanými súbormi, pomenované mergedResult{iterácia}.xml");
string pathMergedXMLDir = UserInputDirToFiles();

string outputFileName = UserAnswerOutputFileName();
string projetDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
string outputPath = Path.Combine(projetDir, "outputs",outputFileName);

while (true)
{
    string pathMergedXML = Path.Combine(pathMergedXMLDir, $"mergedResult{index}.xml");
    string pathGeneratedXML = Path.Combine(pathGeneratedXMLDir, index.ToString(), $"expectedResult{index}.xml");

    if (!FilesExists(pathMergedXML, pathGeneratedXML)) break; //vypne sa ked uz nenajde dvojicu suborov s indexom

    // orezáva biele znaky a odstraňuje prázdné riadky
    string[] expected = File.ReadAllLines(pathGeneratedXML).Select(line => line.Trim()).Where(line => line != "").ToArray();
    string[] merged = File.ReadAllLines(pathMergedXML).Select(line => line.Trim()).Where(line => line != "").ToArray();


    if (!IsValidXml(pathGeneratedXML))
    {
        Console.Error.WriteLine("Veľký problém, XML generátor vytvoril nefungujúci XML");
        return;
    }

    countTotalFiles++;
    Console.WriteLine($"Porovnávám soubory expectedResult{index}.xml a mergedResult{index}.xml");
    if (!IsValidXml(pathMergedXML))
    {
        countNotValidXMLFiles++;
        index++;
        continue;
    }

    // rychla kontrola, či sú súbory úplne rovnaké, ak áno, nemusíme porovnávať elementy a hodnoty
    if (ordersMatters)
    {
        if (AreEqualOrderMatters(expected, merged))
        {
            countCorrectFiles++;
            index++;
            continue;
        }
    }
    else
    {
        if (AreEqualOrderDoesNotMatter(expected, merged))
        {
            countCorrectFiles++;
            index++;
            continue;
        }
    }

    // porovnáme elementy a hodnoty, aby sme zistili, čo presne je zle
    var addedRemovedChangedCounts = GetAddedMissingWrongValueCounts(expected, merged);

    var addedCount = addedRemovedChangedCounts[0];
    var missingCount = addedRemovedChangedCounts[1];
    var wrongValueCount = addedRemovedChangedCounts[2];
    var wrongPositionCount = ordersMatters ? ElementsInWrongPosition(expected, merged) : 0;

    if (addedCount == 0 && missingCount == 0 && wrongValueCount == 0 && wrongPositionCount == 0)
    {
        Console.Error.WriteLine("Nejaká chyba v porovnávaní, súbory nejsou stejné ale neidentifikovali jsme žádný rozdíl");
        countCorrectFiles++;
        continue;
    }
    // hodnoty a index pre vypis do výstupného súboru
    addedElementCounts.Add(addedCount);
    missingElementCounts.Add(missingCount);
    wrongValueCounts.Add(wrongValueCount);
    wrongPositionCounts.Add(wrongPositionCount);
    indexesWithMistakes.Add(index);

    index++;
}

double averageCorrectness = countTotalFiles > 0 ? (double)countCorrectFiles / countTotalFiles * 100 : 0;

string txtOutput = $"\nPorovnaných {countTotalFiles} súborov, z toho \n{countNotValidXMLFiles} nebolo validních XML a \n{countTotalFiles - countCorrectFiles} boli rozdielne.\n" +
    $"{averageCorrectness}% súborov boli rovnaké + validné XML súbory\n\n";

for (int i = 0; i < addedElementCounts.Count; i++)
{
    int sumMistakes = addedElementCounts[i] + missingElementCounts[i] + wrongValueCounts[i] + wrongPositionCounts[i];
    if (sumMistakes == 0) continue;

    txtOutput += $"Súbor {indexesWithMistakes[i]}: Přidané elementy: {addedElementCounts[i]}, Chybějící elementy: {missingElementCounts[i]}, Nesprávné hodnoty: {wrongValueCounts[i]}, Nesprávné pozice: {wrongPositionCounts[i]}\n";
}
Console.Write(txtOutput);
File.WriteAllText(outputPath, txtOutput);

bool UserAnswerOrderMatters()
{
    Console.WriteLine("Zaleží na poradí atribútov/elementov?");
    Console.WriteLine("Odpovedz: yes/no");
    string? input = "";
    // Cyklus pokračuje, dokiaľ nie je zadané "yes" alebo "no"
    while (input != "yes" && input != "no")
    {
        input = Console.ReadLine();
        if (input == null)
        {
            input = "";
            continue;
        }
        input = input.ToLower();
    }
    if (input == "yes") return true;

    return false;    
}

string UserAnswerOutputFileName()
{
    Console.WriteLine("Zadajta meno súboru do ktorého chcete zapísať výsledok:");
    string? input = "";
    // Cyklus pokračuje, dokiaľ nie je zadané "yes" nebo "no"
    while (input == "")
    {
        input = Console.ReadLine();
        if (input == null)
        {
            input = "";
            continue;
        }
        input = input.ToLower() + ".txt";
    }
    return input;
}

int[] GetAddedMissingWrongValueCounts(string[] expected, string[] merged)
{
    var addedElements = merged.Except(expected);
    var missingElements = expected.Except(merged);
    var wrongValueCount = 0;

    foreach (var element in addedElements)
    {
        var label = element.Substring(0, element.IndexOf('>'));
        // Ak removed aj added obsahujú rovnako pomenovaný element, znamená to že daný element má nesprávnu hodnotu
        if (missingElements.Any(r => r.StartsWith(label)))
        {
            wrongValueCount++;
        }
    }
    var addedCount = addedElements.Count() - wrongValueCount;
    var removedCount = missingElements.Count();
 
    var changed = expected.Intersect(merged).Count();
    return new int[] { addedCount, removedCount, wrongValueCount };
}

int ElementsInWrongPosition(string[] expected, string[] merged)
{
    int wrongPositionCount = 0;
    var expectedSet = new HashSet<string>(expected);
    int loops = Math.Min(expected.Length, merged.Length);
    for (int i = 0; i < loops; i++)
    {
        if (expectedSet.Contains(merged[i]) && expected[i] != merged[i])
        {
            wrongPositionCount++;
        }
    }
    return wrongPositionCount;
}

string UserInputDirToFiles()
{
    while (true)
    {
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("Zadajte platnú cestu (nie prázdnu). Skúste znova:");
            continue;
        }

        input = input.Trim().Trim('"');

        try
        {
            string full = Path.GetFullPath(input);

            if (!Directory.Exists(full))
            {
                Console.WriteLine($"Adresár neexistuje: {full}. Skontrolujte cestu a skúste znova:");
                continue;
            }

            return full;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Neplatná cesta: {ex.Message}. Skúste znova:");
        }
    }
}

bool FilesExists(string pathMergedXml, string pathExpectedXML)
{
    if (!File.Exists(pathMergedXml) || !File.Exists(pathExpectedXML))
    {
        return false;
    }
    return true;
}

bool AreEqualOrderMatters(string[] a, string[] b)
{
    return a.SequenceEqual(b);
}

bool AreEqualOrderDoesNotMatter(string[] a, string[] b)
{
    var setA = new HashSet<string>(a);
    var setB = new HashSet<string>(b);
    return setA.SetEquals(setB);
}

bool IsValidXml(string path)
{
    try
    {
        XDocument.Load(path);
        return true;
    }
    catch (System.Xml.XmlException)
    {
        return false;
    }
}

