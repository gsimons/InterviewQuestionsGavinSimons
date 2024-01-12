

using System.Text.Json;


public class JobMeta
{
    public string Job { get; set; }
    public double Rate { get; set; }
    public double BenefitsRate { get; set; }
}

public class TimePunchInfo
{
    public string Job { get; set; }
    public string Start { get; set; }
    public string End { get; set; }
} 
public class EmployeeData
{
    public string Employee { get; set; }
    public List<TimePunchInfo> TimePunch { get; set; }
}

public class Root
{
    public List<JobMeta> JobMeta { get; set; }
    public List<EmployeeData> EmployeeData { get; set; }
}

public class CalculatedPayroll
{
    public string Employee { get; set; }
    public string Regular { get; set; }
    public string Overtime { get; set; }
    public string Doubletime { get; set; }
    public string WageTotal { get; set; }
    public string BenefitTotal { get; set; }
}



public class ConsoleAppTest
{
    static List<JobMeta> jobMetas = new List<JobMeta>();
    static List<EmployeeData> employeeDatas;

    static List<CalculatedPayroll> calculatedPayrolls = new List<CalculatedPayroll>();

    //The hour caps for each rate
    const double RegularHours = 40.0;
    const double OvertimeHours = 8;
    //all time over 48 is Doubletime, don't need a const for it


    //The hourly rates 
    const double RegularRate = 1.0;
    const double OvertimeRate = 1.5;
    const double DoubletimeRate = 2.0;


    static void Main()
    {
        string jsonString = File.ReadAllText("PunchLogicTest.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        Root ?punchCardRoot = JsonSerializer.Deserialize<Root>(jsonString, options);

        if(punchCardRoot == null)
        {
            //some simple error logging
            string jsonError = DateTime.Now.ToString() + " Error: JSON file not deserialized successfully";
            using (StreamWriter sw = File.AppendText("ErrorLog.txt"))
            {
                sw.WriteLine(jsonError);
            }
            Console.WriteLine(jsonError);
        }
        else
        {
            jobMetas = punchCardRoot.JobMeta;
            employeeDatas = punchCardRoot.EmployeeData;

            //iterate over all employees and calculate their payroll data
            foreach (EmployeeData e in employeeDatas)
            {
                calculateEmployeePayroll(e);

            }

            //serialize the calculatedPayrolls object into a JSON string, outputed to the console
            string jsonOutput = JsonSerializer.Serialize(calculatedPayrolls);

            Console.WriteLine(jsonOutput);
        }

    }

    //Calculates all the payroll data for a given employee e
    public static void calculateEmployeePayroll(EmployeeData e)
    {
        //Try catch statement to prevent exceptions from keeping the rest of payroll being calculated
        try {
            CalculatedPayroll cp = new CalculatedPayroll();

            cp.Employee = e.Employee;

            double regular = 0.0000;
            double overTime = 0.0000;
            double doubleTime = 0.0000;
            double wageTotal = 0.0000;
            double benefitTotal = 0.0000;


            //iterate over all the timePunch data for the employee
            foreach (TimePunchInfo tpi in e.TimePunch)
            {

                JobMeta? tpiMeta = jobMetas.Where(x => x.Job == tpi.Job).FirstOrDefault();
                if (tpiMeta != null)
                {

                    var parsedStart = DateTime.Parse(tpi.Start);
                    var parsedEnd = DateTime.Parse(tpi.End);

                    TimeSpan timeWorked = parsedEnd - parsedStart;
                    double hoursWorked = timeWorked.TotalHours;

                    //Start time is after End time, skip the hours and log the error
                    if(hoursWorked < 0)
                    {
                        string timeErrorOutput = DateTime.Now.ToString() + " Error: For employee " + cp.Employee + " the job: " + tpi.Job + " has a Start time that is after its End time, hours for this job will not be counted";
                        using (StreamWriter sw = File.AppendText("ErrorLog.txt"))
                        {
                            sw.WriteLine(timeErrorOutput);
                        }
                        Console.WriteLine(timeErrorOutput);

                        continue;
                    }

                    //Benefits rate does not changed based on hours worked over RegularTime
                    benefitTotal += tpiMeta.BenefitsRate * hoursWorked;

                    var carryOverTime = 0.0;

                    if (regular < RegularHours && hoursWorked > 0)
                    {
                        //Add the job's hours worked to current Regular hours worked
                        var newRegularHours = regular + hoursWorked;

                        //get the amount of hours worked over RegularHours, if any
                        carryOverTime = newRegularHours - RegularHours;
                        if (carryOverTime < 0)
                        {
                            carryOverTime = 0.0;
                        }

                        //Remove potential hours in excess of Regular hours from the new Regular hours total
                        newRegularHours -= carryOverTime;

                        //update the employee's calculatedpayroll with RegularHours time and pay
                        wageTotal += (hoursWorked - carryOverTime) * (RegularRate * tpiMeta.Rate);
                        regular = newRegularHours;

                        //adjust hoursWorked to represent the amount that does not have pay calculated yet
                        hoursWorked = carryOverTime;
                    }

                    if (regular == RegularHours && overTime < OvertimeHours && hoursWorked > 0)
                    {
                        //Add the job's hours worked to current Overtime hours worked
                        var newOvertimeHours = overTime + hoursWorked;

                        //get the amount of hours worked over OvertimeHours, if any
                        carryOverTime = newOvertimeHours - OvertimeHours;
                        if (carryOverTime < 0)
                        {
                            carryOverTime = 0.0;
                        }

                        //Remove potential hours in excess of Overtime hours from the new Overtime hours total
                        newOvertimeHours -= carryOverTime;

                        //update the employee's calculatedpayroll with OvertimeHours time and pay
                        wageTotal += (hoursWorked - carryOverTime) * (OvertimeRate * tpiMeta.Rate);
                        overTime = newOvertimeHours;

                        //adjust hoursWorked to represent the amount that does not have pay calculated yet
                        hoursWorked = carryOverTime;
                    }

                    if (regular == RegularHours && overTime == OvertimeHours && hoursWorked > 0)
                    {
                        //Add the job's hours worked to current Doubletime hours worked
                        var newDoubletimeHours = doubleTime + hoursWorked;

                        //update the employee's calculatedpayroll with DoubletimeHours time and pay
                        wageTotal += hoursWorked * (DoubletimeRate * tpiMeta.Rate);
                        doubleTime = newDoubletimeHours;
                    }

                }
                else
                {
                    //job given in timePunch was not found in the jobMeta, do some simple error logging
                    string jobErrorOutput = DateTime.Now.ToString() + " Error: For employee " + cp.Employee + " the job: " + tpi.Job + " does not exist in given jobmeta, hours for this job will not be counted";
                    using (StreamWriter sw = File.AppendText("ErrorLog.txt"))
                    {
                        sw.WriteLine(jobErrorOutput);
                    }
                    Console.WriteLine(jobErrorOutput);
                }

            }

            //round values to the expected 4 decimals
            double regularRounded = Math.Round(regular, 4);
            double overtimeRounded = Math.Round(overTime, 4);
            double doubleTimeRounded = Math.Round(doubleTime, 4);
            double wageTotalRounded = Math.Round(wageTotal, 4);
            double benefitTotalRounded = Math.Round(benefitTotal, 4);

            //convert calculated values into the expected string values with trailing zeros if needed
            cp.Regular = regularRounded.ToString("0.0000");
            cp.Overtime = overtimeRounded.ToString("0.0000");
            cp.Doubletime = doubleTimeRounded.ToString("0.0000");
            cp.WageTotal = wageTotalRounded.ToString("0.0000");
            cp.BenefitTotal = benefitTotalRounded.ToString("0.0000");

            calculatedPayrolls.Add(cp);
        }
        catch (Exception ex){

            //Add exception to error log (and console for test app purposes) stating which employee payroll couldn't be calculated for
            string employeeErrorOutput = DateTime.Now.ToString() + " Error: For employee " + e.Employee + " payroll could not be calculated due to exception: " + ex.ToString();
            using (StreamWriter sw = File.AppendText("ErrorLog.txt"))
            {
                sw.WriteLine(employeeErrorOutput);
            }
            Console.WriteLine(employeeErrorOutput);
        }

    }




}


