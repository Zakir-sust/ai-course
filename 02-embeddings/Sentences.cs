// Annual-report-style test data (10-K wording). Illustrative, not quoted from a real report.
static class Data
{
    public static readonly string[] Sentences =
    [
        // Revenue (0-4)
        "Total net sales increased 8 percent compared to the prior year, driven by higher sales of smartphones and services.",
        "Services revenue includes advertising, cloud subscriptions, digital content and payment services.",
        "Net sales in the Americas segment grew due to stronger demand for laptops and tablets.",
        "Revenue is recognized when control of the promised goods or services is transferred to the customer.",
        "Foreign currency fluctuations reduced reported revenue by about 2 percent during the year.",

        // Expenses (5-9)
        "Research and development expense rose 15 percent as the company invested in artificial intelligence features.",
        "Selling, general and administrative expense includes marketing, sales staff and corporate overhead.",
        "Cost of sales consists of component costs, manufacturing, freight and warranty costs.",
        "Depreciation expense increased because of new data centers and servers placed in service.",
        "Restructuring charges relate to severance payments and the closure of two offices.",

        // People (10-14)
        "As of the end of the fiscal year, the company had approximately 160,000 full-time equivalent employees.",
        "The company offers equity awards, health benefits and paid parental leave to attract and retain talent.",
        "Competition for engineers with machine learning skills is intense and could raise compensation costs.",
        "Employees in some European countries are represented by works councils.",
        "The company relies on independent contractors and temporary staff during the holiday season.",

        // Risks and properties (15-19)
        "The company depends on a small number of suppliers in Asia for key components, and a disruption could delay products.",
        "Cyberattacks or data breaches could harm the company's reputation and lead to regulatory fines.",
        "The company's headquarters is located in California, and it leases office space in more than 40 countries.",
        "Changes in tax laws or trade tariffs could increase costs and reduce profit margins.",
        "The company is subject to antitrust investigations and lawsuits in the United States and the European Union.",
    ];

    // Wording deliberately avoids the target sentence. Swap in your own if you like.
    public static readonly (string Text, int Expected)[] Queries =
    [
        ("How many people work there?", 10),
        ("What would happen if a factory overseas shut down?", 15),
        ("Why are the costs of hiring AI experts going up?", 12),
        ("Does subscription income for software count, and when is it booked?", 1),
        ("What's the definition of services revenue?", 1)
    ];
}
