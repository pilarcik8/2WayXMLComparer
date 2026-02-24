using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;


// pro ukládání počtu přidaných, chybějících a špatně umístěných elementů pro každý soubor
string textMistakesLogger = "";

// Počet nevalidních XML, celkových souborů a správných souborů
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

int index = 0;
while (true)
{
    string pathMergedXML = Path.Combine(pathMergedXMLDir, $"mergedResult{index}.xml");
    string pathGeneratedXML = Path.Combine(pathGeneratedXMLDir, index.ToString(), $"expectedResult{index}.xml");

    if (!FilesExists(pathMergedXML, pathGeneratedXML)) break; //vypne sa ked uz nenajde dvojicu suborov s indexom

    // orezáva biele znaky a odstraňuje prázdné riadky
    string[] generated = File.ReadAllLines(pathGeneratedXML).Select(line => line.Trim()).Where(line => line != "").ToArray();
    string[] merged = File.ReadAllLines(pathMergedXML).Select(line => line.Trim()).Where(line => line != "").ToArray();

    if (generated.Distinct().Count() != generated.Count())
    {
        Console.Error.WriteLine("Generovaný súbor má v sebe duplicity, ciže ´v generátore nastala chyba");
        return;
    }

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
        if (AreEqualOrderMatters(generated, merged))
        {
            countCorrectFiles++;
            index++;
            continue;
        }
    }
    else
    {
        if (AreEqualOrderDoesNotMatter(generated, merged))
        {
            countCorrectFiles++;
            index++;
            continue;
        }
    }

    // porovnáme elementy a hodnoty, aby sme zistili, čo presne je zle
    var addedElements = merged.Except(generated);
    var missingElements = generated.Except(merged);
    var addedCount = addedElements.Count();
    var missingCount = missingElements.Count();
    var wrongPositionCount = ordersMatters ? ElementsInWrongPosition(generated, merged) : 0;
    var hasDuplicates = merged.Length != merged.Distinct().Count();

    // kontrola či naozaj existuje rozdiel
    if (addedCount == 0 && missingCount == 0 && wrongPositionCount == 0 && !hasDuplicates)
    {
        Console.Error.WriteLine("Nejaká chyba v porovnávaní, súbory nejsou stejné ale neidentifikovali jsme žádný rozdíl");
        countCorrectFiles++;
        continue;
    }

    index++;
    string addedElementsStr = addedElements.Any() ? string.Join(", ", addedElements) : "žiadne";
    string missingElementsStr = missingElements.Any() ? string.Join(", ", missingElements) : "žiadne";

    textMistakesLogger += 
        $"mergedResult{index}.xml:" +
        $"\nPřidané elementy: {addedElementsStr}," +
        $"\nChybějící elementy: {missingElementsStr}," +
        $"\nPočet nesprávne umiestnených elementov: {wrongPositionCount}" + 
        $"\nMá duplikácie: {hasDuplicates}\n\n";
}

// vypis + file output
double averageCorrectness = countTotalFiles > 0 ? (double)countCorrectFiles / countTotalFiles * 100 : 0;

string txtOutput = $"\nPorovnaných {countTotalFiles} súborov, z toho \n{countNotValidXMLFiles} nebolo validních XML a \n{countTotalFiles - countCorrectFiles} boli rozdielne.\n" +
    $"{averageCorrectness}% súborov boli rovnaké + validné XML súbory\n\n";
txtOutput += textMistakesLogger;
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
    // dôvod: pri tvorbe setu sa ignorovajú duplikáty 
    if (a.Length != b.Length)
    {
        return false;
    }

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

