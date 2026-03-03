using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;


// pre ukladanie počtu pridaných, chýbajúcich a nesprávne umiestnených elementov pre každý súbor
string textMistakesLogger = "";

// Počet nevalidných XML, celkových súborov a správnych súborov
int countNotValidXMLFiles = 0;
int countTotalFiles = 0;
int countCorrectFiles = 0;
int fileNotFoundCount = 0; // počítadlo nenájdených súborov keď používateľ zadal počet iterácií


bool ordersMatters = UserAnswerOrderMatters();
Console.WriteLine("Zadajte absolútnu cestu k priečinkom (pomenujte ich 0, 1, 2...) obsahujúcim súbor expectedResult{iterácia}.xml");
string pathGeneratedXMLDir = UserInputDirToFiles();
Console.WriteLine("Zadajte absolútnu cestu k priečinku s generovanými (merged) súbormi, pomenované mergedResult{iterácia}.xml");
string pathMergedXMLDir = UserInputDirToFiles();

Console.WriteLine("Zadajte počet iterácií (prázdne = pokračovať dokedy sú súbory)");
string? iterInput = Console.ReadLine();
int maxIteration = -1;
if (!string.IsNullOrWhiteSpace(iterInput))
{
    if (!int.TryParse(iterInput.Trim(), out maxIteration))
    {
        Console.WriteLine("Neplatné číslo, bude pokračovať ako default (dokedy sú súbory).");
        maxIteration = -1;
    }
    maxIteration--; // pretože index začíná od 0, ale uživatel zadává počet iterací od 1
}

string outputFileName = UserAnswerOutputFileName();
string projetDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
string outputPath = Path.Combine(projetDir, "outputs", outputFileName);

int index = 0;
while (maxIteration <= -1 ? true : index <= maxIteration)
{
    string pathMergedXML = Path.Combine(pathMergedXMLDir, $"mergedResult{index}.xml");
    string pathGeneratedXML = Path.Combine(pathGeneratedXMLDir, index.ToString(), $"expectedResult{index}.xml");

    // Keď používateľ nezadal maxIteration, pôvodné správanie: skonči keď chýba pár súborov.
    // Keď používateľ zadal maxIteration, počítaj nenájdené súbory a pokračuj až do požadovanej iterácie.
    if (!File.Exists(pathMergedXML) || !File.Exists(pathGeneratedXML))
    {
        if (maxIteration == -1)
        {
            break; // pôvodné správanie
        }
        else
        {
            fileNotFoundCount++;
            index++;
            continue;
        }
    }

    // orezáva biele znaky a odstraňuje prázdne riadky
    string[] generated = File.ReadAllLines(pathGeneratedXML).Select(line => line.Trim()).Where(line => line != "").ToArray();
    string[] merged = File.ReadAllLines(pathMergedXML).Select(line => line.Trim()).Where(line => line != "").ToArray();

    if (generated.Distinct().Count() != generated.Count())
    {
        Console.Error.WriteLine("Generovaný súbor obsahuje duplicity — chyba v generátore.");
        return;
    }

    if (!IsValidXml(pathGeneratedXML))
    {
        Console.Error.WriteLine("Veľký problém: generátor vytvoril nevalidné XML.");
        return;
    }

    countTotalFiles++;
    Console.WriteLine($"Porovnávam súbory expectedResult{index}.xml a mergedResult{index}.xml");
    if (!IsValidXml(pathMergedXML))
    {
        countNotValidXMLFiles++;
        index++;
        continue;
    }

    // hlavička nemá byť case-sensitive, ale zvyšok áno
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
        Console.Error.WriteLine("Nastala neočakávaná chyba v porovnávaní: súbory nie sú rovnaké, ale žiadny rozdiel nebol identifikovaný.");
        countCorrectFiles++;
        index++;
        continue;
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

// výpis a uloženie do súboru
double averageCorrectness = countTotalFiles > 0 ? (double)countCorrectFiles / countTotalFiles * 100 : 0;

string txtOutput = $"\nPorovnaných {countTotalFiles} súborov, z toho \n{countNotValidXMLFiles} nebolo validných XML, \n{fileNotFoundCount} súborov nebolo nájdených počas požadovaných iterácií a \n{countTotalFiles - countCorrectFiles} boli rozdielne.\n" +
    $"{averageCorrectness}% súborov boli rovnaké a validné\n\n";
txtOutput += textMistakesLogger;
Console.Write(txtOutput);

Directory.CreateDirectory(Path.Combine(projetDir, "outputs"));
File.WriteAllText(outputPath, txtOutput);
Console.WriteLine($"Výsledky zapísané do: {outputPath}");

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
        Console.Error.WriteLine("Neplatný alebo neexistujúci priečinok so generovanými XML — kópia nebola uložená.");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Nepodarilo sa uložiť kópiu do generovaného adresára: {ex.Message}");
}

bool UserAnswerOrderMatters()
{
    Console.WriteLine("Záleží na poradí atribútov/elementov?");
    Console.WriteLine("Odpoveď: yes/no");
    string? input = "";
    // cyklus pokračuje, dokiaľ nie je zadané "yes" alebo "no"
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
    return input == "yes";
}

string UserAnswerOutputFileName()
{
    Console.WriteLine("Zadajte meno súboru, do ktorého chcete zapísať výsledok:");
    string? input = "";
    // cyklus pokračuje, dokiaľ nie je zadané meno súboru
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

bool AreEqualOrderMatters(string[] a, string[] b)
{
    return a.SequenceEqual(b);
}

bool AreEqualOrderDoesNotMatter(string[] a, string[] b)
{
    // pri tvorbe množiny sa ignorujú duplikáty, preto najprv porovnáme dĺžky
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

