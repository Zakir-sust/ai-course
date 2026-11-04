// Treaty-style test data. Wording is illustrative, not quoted from a real treaty.
static class Data
{
    public static readonly string[] Sentences =
    [
        // Dividends (0-4)
        "Dividends paid by a company resident in one State to a resident of the other State may be taxed in that other State.",
        "The tax charged on dividends shall not exceed 5 percent of the gross amount if the beneficial owner holds at least 25 percent of the capital.",
        "In all other cases the tax on dividends shall not exceed 15 percent of the gross amount.",
        "The term dividends means income from shares or other rights participating in profits, not being debt claims.",
        "Profits distributed by a company to its shareholders are treated as dividends under the law of the paying company's State.",

        // Interest (5-9)
        "Interest arising in one State and paid to a resident of the other State may be taxed in that other State.",
        "The tax charged on interest shall not exceed 10 percent of the gross amount of the interest.",
        "The term interest means income from debt claims of every kind, including government securities and bonds.",
        "Interest paid to the government or central bank of the other State shall be exempt from tax in the State where it arises.",
        "Penalty charges for late payment shall not be regarded as interest for the purposes of this Article.",

        // Royalties (10-14)
        "Royalties arising in one State and beneficially owned by a resident of the other State shall be taxable only in that other State.",
        "The term royalties means payments for the use of any copyright, patent, trade mark, design, secret formula or process.",
        "Payments for the use of industrial, commercial or scientific equipment are included in the definition of royalties.",
        "Where the royalties exceed an arm's length amount because of a special relationship, this Article applies only to the arm's length amount.",
        "Royalties are deemed to arise in a State when the payer is a resident of that State.",

        // Residence and permanent establishment (15-19)
        "A resident of a State means any person who is liable to tax there by reason of domicile, residence or place of management.",
        "An individual resident in both States shall be deemed a resident of the State in which a permanent home is available.",
        "The term permanent establishment means a fixed place of business through which the business of an enterprise is wholly or partly carried on.",
        "A building site or construction project is a permanent establishment only if it lasts more than twelve months.",
        "The profits of an enterprise are taxable only in its home State unless it carries on business through a permanent establishment in the other State.",
    ];

    // Wording deliberately avoids the target sentence. Swap in your own if you like.
    public static readonly (string Text, int Expected)[] Queries =
    [
        ("How much can be withheld when a parent company owns a large stake in its subsidiary?", 1),
        ("Does a construction job abroad for six months create a taxable presence?", 18),
        ("Is software licensing or brand usage fee income covered, and what counts?", 11),
        ("What's the definition of royalties?", 11)
    ];
}
