using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;

namespace NormalizeCreditCard
{
    class Program
    {
        public static void StandardizeStatement(string inputFile, string outputFile)
        {
            Console.WriteLine(String.Format("Input File Path:  {0}",inputFile));
            Console.WriteLine(String.Format("Output File Path: {0}",outputFile));

            //Check Existing Files
            //Input File must exist
            if (!File.Exists(inputFile))
            {
                throw new FileNotFoundException(String.Format("{0} does not exist", inputFile));
            }
            //Delete Output File if exists
            if (File.Exists(outputFile))
            {
                Console.WriteLine("File Already Exists at Path. Deleting Existing File");
                File.Delete(outputFile);
            }

            string[] finalHeader = { "Date", "Transaction Description", "Debit", "Credit", "Currency", "CardName", "Transaction", "Location" };

            //Define Variables and Flags for Parse Logic
            string transaction = "UNDEFINED";
            string cardName = "UNDEFINED";
            string[] currentHeader = new string[0];

            using (FileStream fs2 = File.Create(outputFile))
            {
                //Write Final Header to Output File
                appendRow(fs2, finalHeader);

                //Iterate through Each Row
                IEnumerable<string> lines = File.ReadLines(inputFile);
                foreach(var rowString1 in lines)
                {
                    var rowString = rowString1.Trim();
                    string[] cells = rowString.Split(',');
                    int countNonEmpty = 0;
                    string tempCell = "";


                    bool[] isHeader = { false, false };

                    foreach (var cellI in cells)
                    {
                        var cell = cellI.Trim();
                        //Checks for TransactionScope and CardName
                        if (!String.IsNullOrEmpty(cell))
                        {
                            countNonEmpty++;
                            tempCell = cell;
                        }

                        //Checks for header row
                        if (cell.Equals("Date", StringComparison.OrdinalIgnoreCase))
                        {
                            isHeader[0] = true;
                        }
                        else if (cell.ToLower().StartsWith("transaction de"))
                        {
                            isHeader[1] = true;
                        }
                    }

                    //If isHeader
                    if (isHeader[0] && isHeader[1])
                    {
                        currentHeader = cells;
                        for (int i = 0; i < currentHeader.Length; i++)
                        {
                            currentHeader[i] = currentHeader[i].Trim().ToLower();
                        }
                        continue;
                    }

                    //If only 1 non-empty element, either TransactionScope or CardName
                    if (countNonEmpty == 0)
                    {
                        continue;
                    }
                    else if (countNonEmpty == 1)
                    {
                        if (tempCell.Equals("Domestic Transactions", StringComparison.OrdinalIgnoreCase))
                        {
                            transaction = "Domestic";
                        }
                        else if (tempCell.Equals("International Transactions", StringComparison.OrdinalIgnoreCase))
                        {
                            transaction = "International";
                        }
                        else
                        {
                            cardName = tempCell;
                        }
                        continue;
                    }

                    //Data Row

                    //Check if header exists
                    if(currentHeader.Length == 0)
                    {
                        throw new Exception("Header Undefined for Data Row");
                    }

                    OrderedDictionary rowDict = new OrderedDictionary();
                    foreach (var colName in finalHeader)
                    {
                        rowDict.Add(colName,"");
                    }

                    rowDict["CardName"] = cardName;
                    rowDict["Transaction"] = transaction;
                    for (int i = 0; i < cells.Length; i++)
                    {
                        var cell = cells[i].Trim();
                        var colName = currentHeader[i];

                        if (colName.Equals("date"))
                        {
                            rowDict["Date"] = parseDate(cell);
                        }
                        else if(colName.Equals("amount"))
                        {
                            var tempAmount = parseAmount(cell);
                            rowDict["Debit"] = tempAmount[0];
                            rowDict["Credit"] = tempAmount[1];
                        }
                        else if (colName.Equals("debit"))
                        {
                            rowDict["Debit"] = assertNumerical(cell);
                        }
                        else if (colName.Equals("credit"))
                        {
                            rowDict["Credit"] = assertNumerical(cell);
                        }
                        else if (colName.StartsWith("transaction de"))
                        {
                            var tempTrans = parseTransaction(cell,transaction);
                            rowDict["Transaction Description"] = tempTrans[0].Trim();
                            rowDict["Location"] = tempTrans[1];
                            rowDict["Currency"] = tempTrans[2];
                        }
                    }
                    appendRow(fs2, rowDict);
                }
            }
        }

        private static void appendRow(FileStream fs, String[] rowArray)
        {
            String rowData = String.Join(",",rowArray);
            byte[] rowBytes = new UTF8Encoding(true).GetBytes(rowData);
            fs.Write(rowBytes, 0, rowBytes.Length);
            byte[] newline = Encoding.ASCII.GetBytes(Environment.NewLine);
            fs.Write(newline, 0, newline.Length);
        }

        private static void appendRow(FileStream fs, OrderedDictionary rowDict)
        {
            IDictionaryEnumerator rowEnum = rowDict.GetEnumerator();
            String[] rowArray = new string[rowDict.Count];
            int i = 0;
            while (rowEnum.MoveNext())
            {
                rowArray[i] = rowEnum.Value.ToString();
                i++;
            }
            appendRow(fs, rowArray);
        }

        private static String assertNumerical(string cell)
        {
            if(String.IsNullOrEmpty(cell))
            {
                return "0";
            }
            return Convert.ToDouble(cell).ToString();
        }

        //Returns [ Transaction Description, Location, Currency ]
        private static String[] parseTransaction(string cell, string transaction)
        {
            string[] result = new string[3];
            if(transaction.StartsWith("International"))
            {
                string[] temp = cell.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                result[0] = string.Join(" ", temp.Take(temp.Length - 1));
                result[1] = temp[temp.Length - 2];
                result[2] = temp[temp.Length-1];
            }
            else
            {
                string[] temp = cell.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                result[0] = string.Join(" ", temp.Take(temp.Length));
                result[1] = temp[temp.Length - 1];
                result[2] = "INR";
            }
            return result;
        }

        //Returns [ Debit, Credit ]
        private static String[] parseAmount(string cell)
        {
            string[] result = new string[2];
            string[] temp = cell.Split(' ');
            if(temp.Length == 1)
            {
                result[0] = assertNumerical(temp[0]);
                result[1] = "0";
            }
            else
            {
                result[0] = "0";
                result[1] = assertNumerical(temp[0]);
            }
            return result;
        }

        //Return String of dd-MM-yyyy represented date
        private static String parseDate(string cell)
        {
            string resDate = "";
            CultureInfo culture = new CultureInfo("en-GB", false);
            try
            {
                string[] formats = { "d-M-yyyy", "d-M-yy" };
                resDate = DateTime.ParseExact(cell, formats, culture).ToString("dd-MM-yyyy");
            }
            catch (System.FormatException)
            {
                resDate = DateTime.Parse(cell).ToString("dd-MM-yyyy");
            }

            return resDate;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Enter Input File ABSOLUTE Path");
            string inputPath = Console.ReadLine();
            Console.WriteLine("Enter Output Directory ABSOLUTE Path");
            string outputPath = Console.ReadLine();

            //Format file paths
            var inputFileName = Path.GetFileName(inputPath);
            if(inputFileName.IndexOf("input", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new Exception("Input File does not contain string \"Input\"");
            }
            var outputFileName = Regex.Replace(inputFileName, "input", "Output", RegexOptions.IgnoreCase);
            outputPath += String.Format("/{0}", outputFileName);

            StandardizeStatement(inputPath, outputPath);

            Console.WriteLine("Finished");
        }
    }
}
