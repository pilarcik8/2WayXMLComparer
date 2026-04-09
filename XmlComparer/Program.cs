using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

string textMistakesLogger = "";

int countCorrectFiles = 0;
int fileNotFoundCount = 0;
List<int> invalidXmls = new List<int>();

bool ordersMatters = UserAnswerOrderMatters();
string pathGeneratedXMLDir = UserInputDirToFiles("Zadajte absolútnu cestu k priečinkom (pomenujte ich 0, 1, 2...) obsahujúcim súbor expectedResult{iterácia}.xml");
string pathMergedXMLDir = UserInputDirToFiles("Zadajte absolútnu cestu k priečinku s generovanými (merged) súbormi, pomenované mergedResult{iterácia}.xml");
int maxIteration = UserInputMaxIter();
string outputFileName = UserAnswerOutputFileName();

int index = 0;

while (maxIteration <= -1 ? true : index <= maxIteration)
{
    string pathMergedXML = Path.Combine(pathMergedXMLDir, $"mergedResult{index}.xml");
    string pathGeneratedXML = Path.Combine(pathGeneratedXMLDir, index.ToString(), $"expectedResult{index}.xml");

    if (!File.Exists(pathMergedXML) || !File.Exists(pathGeneratedXML))
    {
        if (maxIteration == -1) break;
        if (!File.Exists(pathGeneratedXML))
        {
            throw new Exception($"Očakávaný výsledok nebol nalezený: {pathGeneratedXML}\n. Iterácia mala skončiť na čísle: {maxIteration}");
        }
        index++;
        fileNotFoundCount++;
        continue;
    }

    if (!IsValidXml(pathGeneratedXML))
    {
        throw new Exception("Očakávaný výsledokje nevalidný XML súbor.");
    }

    // orezáva biele znaky a odstraňuje prázdne riadky
    string[] generated = File.ReadAllLines(pathGeneratedXML).Select(line => line.Trim()).Where(line => line != "").ToArray();
    string[] merged = File.ReadAllLines(pathMergedXML).Select(line => line.Trim()).Where(line => line != "").ToArray();

    if (generated.Distinct().Count() != generated.Count())
    {
        throw new Exception("Očakávaný výsledok obsahuje duplicity — chyba v generátore.");
    }

    Console.WriteLine($"Porovnávam súbory expectedResult{index}.xml a mergedResult{index}.xml");
    if (!IsValidXml(pathMergedXML))
    {
        invalidXmls.Add(index);
        index++;
        continue;
    }

    // dôvod: xmldiff robí rozdielne hlavičky
    generated[0] = generated[0].ToLower();
    merged[0] = merged[0].ToLower();

    generated[0] = generated[0].Replace("'", "\"");
    merged[0] = merged[0].Replace("'", "\"");

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
        throw new Exception("Nastala neočakávaná chyba v porovnávaní: súbory nie sú rovnaké, ale žiadny rozdiel nebol identifikovaný.");
    }

    string addedElementsStr = addedElements.Any() ? string.Join(", ", addedElements) : "žiadne";
    string missingElementsStr = missingElements.Any() ? string.Join(", ", missingElements) : "žiadne";

    textMistakesLogger +=
        $"mergedResult{index}.xml:" +
        $"\nPridané elementy: {addedElementsStr}," +
        $"\nChýbajúce elementy: {missingElementsStr}," +
        $"\nPočet nesprávne umiestnených elementov: {wrongPositionCount}" +
        $"\nObsahuje duplikáty: {hasDuplicates}\n\n";

    index++;
}
int countTotalFiles = index;

// výpis a uloženie do súboru
int countNotValidXMLFiles = invalidXmls.Count;
double averageCorrectness = countTotalFiles > 0 ? (double)countCorrectFiles / countTotalFiles * 100 : 0;
int validButDifferentCount = countTotalFiles - (countNotValidXMLFiles + fileNotFoundCount + countCorrectFiles);

string stats = $"\nPorovnaných {countTotalFiles} súborov, z toho \n{countNotValidXMLFiles} nebolo validných XML, \n{fileNotFoundCount} súborov nebolo nájdených počas požadovaných iterácií a \n{validButDifferentCount} boli rozdielne.\n" +
    $"{averageCorrectness}% súborov boli rovnaké a validné\n\n";
string txtOutput = stats;

if (countNotValidXMLFiles > 0)
{
    string invalidFilesStr = string.Join(", ", invalidXmls);
    textMistakesLogger += $"Nevalidné XML súbory: {invalidFilesStr}\n\n";
}

txtOutput += textMistakesLogger;
Console.WriteLine(textMistakesLogger);
Console.WriteLine(stats);

try
{
    if (!string.IsNullOrWhiteSpace(pathGeneratedXMLDir) && Directory.Exists(pathGeneratedXMLDir))
    {
        string copyPath = Path.Combine(pathGeneratedXMLDir, outputFileName);
        File.WriteAllText(copyPath, txtOutput);
        Console.WriteLine($"Kópia výsledkov uložená do: {copyPath}");
    }
    else
    {
        Console.Error.WriteLine("Neplatný alebo neexistujúci priečinok so generovanými XML — kópia txt vysledku nebola uložená.");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Nepodarilo sa uložiť kópiu do generovaného adresára: {ex.Message}");
}

bool UserAnswerOrderMatters()
{
    Console.WriteLine("Záleží na poradí atribútov/elementov?");
    string? input = "";

    while (input != "ano" && input != "nie")
    {
        Console.WriteLine("Odpoveď: ano/nie");
        input = Console.ReadLine();
        if (input == null)
        {
            input = "";
            continue;
        }
        input = input.ToLower();
    }
    return input == "ano";
}

string UserAnswerOutputFileName()
{
    Console.WriteLine("Zadajte meno súboru, do ktorého chcete zapísať výsledok (bez prípony):");
    string? input = "";

    while (string.IsNullOrEmpty(input))
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

string UserInputDirToFiles(string startingMessage)
{
    Console.WriteLine(startingMessage);
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

int UserInputMaxIter()
{
    int maxIteration;
    Console.WriteLine("Zadajte počet iterácií (prázdne = pokračovať dokedy sú súbory)");
    string? iterInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(iterInput))
    {
        Console.WriteLine("Posledná iterácia nastane keď sa nenájde súbor.");
        return -1;
    }

    if (!int.TryParse(iterInput!.Trim(), out maxIteration))
    {
        Console.WriteLine("Nečíselný vstup.");
        Console.WriteLine("Posledná iterácia nastane keď sa nenájde súbor.");
        return -1;
    }

    if (maxIteration <= 0)
    {
        Console.WriteLine("Zvolená príliš nízky počet iterácií.");
        Console.WriteLine("Posledná iterácia nastane keď sa nenájde súbor.");
        return -1;
    }

    maxIteration--; // predpokladáme, že užívateľ zadá počet iterácií počítaný od 1, ale v kódé počítáme od 0
    return maxIteration;    
}

bool AreEqualOrderMatters(string[] a, string[] b)
{
    return a.SequenceEqual(b);
}

bool AreEqualOrderDoesNotMatter(string[] a, string[] b)
{
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

