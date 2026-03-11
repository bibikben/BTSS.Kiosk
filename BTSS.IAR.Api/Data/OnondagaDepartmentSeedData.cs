namespace BTSS.IAR.Api.Data;
public static class OnondagaDepartmentSeedData
{
    public sealed record DepartmentSeed(string Code, string Name, string MainAddress, DepartmentStationSeed[] Stations);
    public sealed record DepartmentStationSeed(string Number, string Address);
    public static readonly DepartmentSeed[] All = new DepartmentSeed[]
    {
        new("01", "Amber V.F.D. & Amb.", "2216 Amber Rd. between Otisco Valley Rd. & Kinyon Rd.,Marietta (KYG533)", new DepartmentStationSeed[]
        {
            new("1", "2216 Amber Rd. between Otisco Valley Rd. & Kinyon Rd.,Marietta (KYG533)"),
        }),
        new("02", "Apulia V.F.C. Inc.", "6441 State Route 80 between Apulia Rd. & Sky High Rd.,Apulia Station (KZV240)", new DepartmentStationSeed[]
        {
            new("1", "6441 State Route 80 between Apulia Rd. & Sky High Rd.,Apulia Station (KZV240)"),
        }),
        new("03", "Baldwinsville V.F.D.", "7911 Crego Rd. between Downer St. Rd. (aka State Route 31) & Tappan St. Rd.,Baldwinsville (KQP693)", new DepartmentStationSeed[]
        {
            new("1", "7911 Crego Rd. between Downer St. Rd. (aka State Route 31) & Tappan St. Rd.,Baldwinsville (KQP693)"),
            new("2", "7461 State Fair Blvd. (aka State Route 48) between O'Brien Rd. & Village Blvd. S.,Baldwinsville (Sta. is shared with the Lakeside VFD) (KNAI748)"),
            new("3", "59 Elizabeth St. between Mechanic St. & Dead End,Baldwinsville (Sta. #3 sits at the Dead End)"),
        }),
        new("04", "Belgium Cold Springs V.F.D.", "8435 Loop Rd. between W. Entry Rd. (aka State Route 631) & State Route 31 (aka Belgium Rd.),Baldwinsville", new DepartmentStationSeed[]
        {
            new("ADMIN", "8435 Loop Rd. between W. Entry Rd. (aka State Route 631) & State Route 31 (aka Belgium Rd.),Baldwinsville"),
            new("1", "7920 River Rd. @ Patchett Rd.,Baldwinsville (KLR348)"),
            new("2", "8451 Loop Rd. @ W. Entry Rd. (aka State Route 631),Baldwinsville (KUZ710)"),
        }),
        new("05", "Borodino V.F.D.", "2500 Nunnery Rd. between Bockes Rd. & Littlehales Ln.,Skaneateles (KZV246)", new DepartmentStationSeed[]
        {
            new("1", "2500 Nunnery Rd. between Bockes Rd. & Littlehales Ln.,Skaneateles (KZV246)"),
        }),
        new("07", "Bridgeport V.F.D.", "427 State Route 31 (aka Lake Rd.) between Bridgeport Kirkville Rd. (aka County Route 1) & Petrie Rd.,Bridgeport (KSQ722) (Madison Co.)", new DepartmentStationSeed[]
        {
            new("1", "427 State Route 31 (aka Lake Rd.) between Bridgeport Kirkville Rd. (aka County Route 1) & Petrie Rd.,Bridgeport (KSQ722) (Madison Co.)"),
            new("2", "2219 State Route 31 (aka Lake Rd.) @ Jefferson Ave.,Canastota (Madison Co.)"),
        }),
        new("08", "Camillus V.F.D.", "5801 Newport Rd. @ Scenic Dr.,Camillus (KSZ853)", new DepartmentStationSeed[]
        {
            new("1", "5801 Newport Rd. @ Scenic Dr.,Camillus (KSZ853)"),
        }),
        new("09", "Brewerton V.F.D. & Amb.", "9625 Brewerton Rd. (aka US-11) between Jerome St. & Willow St.,Brewerton (KQP692)", new DepartmentStationSeed[]
        {
            new("1", "9625 Brewerton Rd. (aka US-11) between Jerome St. & Willow St.,Brewerton (KQP692)"),
            new("2", "6352 Muskrat Bay Rd. between Long Point Rd. & Ladd Rd.,Brewerton (KNAI747)"),
        }),
        new("10", "Cicero V.F.D.", "8377 Brewerton Rd. (aka US-11) @ State Route 31,Cicero (KQP691) (The Current (43.176285/-76.120093) Sta. shares the same address as the Former (43.176062/-76.119863) Sta.)", new DepartmentStationSeed[]
        {
            new("1", "8377 Brewerton Rd. (aka US-11) @ State Route 31,Cicero (KQP691) (The Current (43.176285/-76.120093) Sta. shares the same address as the Former (43.176062/-76.119863) Sta.)"),
            new("2", "6109 State Route 31 @ Damon Rd.,Cicero (WNZB708)"),
        }),
        new("11", "Clay V.F.D.", "4383 State Route 31 between Morgan Rd. & Henry Clay Blvd.,Clay (KWQ556) (Was known as Sta. #2)", new DepartmentStationSeed[]
        {
            new("1", "4383 State Route 31 between Morgan Rd. & Henry Clay Blvd.,Clay (KWQ556) (Was known as Sta. #2)"),
            new("3", "8129 Caughdenoy Rd. between Maple Rd. & Mirage Ln.,Clay (KWQ555)"),
        }),
        new("13", "Delphi Falls V.F.D.", "2260 Oran Delphi Rd. @ Fairport Rd.,Delphi Falls (KZV241)", new DepartmentStationSeed[]
        {
            new("1", "2260 Oran Delphi Rd. @ Fairport Rd.,Delphi Falls (KZV241)"),
        }),
        new("14", "Dewitt V.F.D.", "4500 E. Genesee St. (aka State Route 5/State Route 92) @ Wellington Rd. @ across from Erie Blvd. E. (aka State Route 5),Dewitt (KLR341/WPFX829)", new DepartmentStationSeed[]
        {
            new("1", "4500 E. Genesee St. (aka State Route 5/State Route 92) @ Wellington Rd. @ across from Erie Blvd. E. (aka State Route 5),Dewitt (KLR341/WPFX829)"),
            new("2", "148 Sanders Creek Pkwy. between Kinne St. & Creek Cir.,East Syracuse (KKR438)"),
        }),
        new("15", "East Syracuse V.F.D.", "204 N. Center St. between E. Manlius St. (aka State Route 290) & E. Ellis St.,East Syracuse (KLR344) (The bays face the cross streets)", new DepartmentStationSeed[]
        {
            new("1", "204 N. Center St. between E. Manlius St. (aka State Route 290) & E. Ellis St.,East Syracuse (KLR344) (The bays face the cross streets)"),
        }),
        new("17", "Elbridge V.F.D.", "275 E. Main St. (aka State Route 5) between Sandbank Rd. & Kingston Rd.,Elbridge (KQP689)", new DepartmentStationSeed[]
        {
            new("1", "275 E. Main St. (aka State Route 5) between Sandbank Rd. & Kingston Rd.,Elbridge (KQP689)"),
        }),
        new("18", "Fabius V.F.D.", "7825 Main St. (aka State Route 80) between Petit St. & Smith St.,Fabius (KOP993)", new DepartmentStationSeed[]
        {
            new("1", "7825 Main St. (aka State Route 80) between Petit St. & Smith St.,Fabius (KOP993)"),
        }),
        new("19", "Fairmount V.F.D.", "4611 W. Genesee St. between Turner Ave. & Male Ave.,Syracuse (KLK513)", new DepartmentStationSeed[]
        {
            new("1", "4611 W. Genesee St. between Turner Ave. & Male Ave.,Syracuse (KLK513)"),
        }),
        new("20", "Fayetteville V.F.D. & Amb.", "425 E. Genesee St. (aka State Route 5) @ N. Manlius St. (aka State Route 257),Fayetteville (KSQ723) (The fire bays face N. Manlius St. while the ambulance bays face E. Genesee St.)", new DepartmentStationSeed[]
        {
            new("1", "425 E. Genesee St. (aka State Route 5) @ N. Manlius St. (aka State Route 257),Fayetteville (KSQ723) (The fire bays face N. Manlius St. while the ambulance bays face E. Genesee St.)"),
        }),
        new("21", "Hinsdale V.F.D.", "113 Malden Rd. between Brewerton Rd. (aka US-11) & Wright Ave.,Mattydale (KZV251)", new DepartmentStationSeed[]
        {
            new("1", "113 Malden Rd. between Brewerton Rd. (aka US-11) & Wright Ave.,Mattydale (KZV251)"),
        }),
        new("22", "Howlett Hill V.F.D.", "3384 Howlett Hill Rd. @ Beef St.,Syracuse (KQP685)", new DepartmentStationSeed[]
        {
            new("1", "3384 Howlett Hill Rd. @ Beef St.,Syracuse (KQP685)"),
        }),
        new("23", "Jamesville V.F.D.", "6661 E. Seneca Trpk. (aka State Route 173) between State Route 91 & Taylor Rd.,Jamesville (KSQ724)", new DepartmentStationSeed[]
        {
            new("1", "6661 E. Seneca Trpk. (aka State Route 173) between State Route 91 & Taylor Rd.,Jamesville (KSQ724)"),
        }),
        new("24", "Jordan V.F.D. & Amb.", "1 N. Hamilton St. @ McLaughlin Dr.,Jordan (KSQ725) (The fire bays face N. Hamilton St. while the ambulance bays face McLaughlin Dr.)", new DepartmentStationSeed[]
        {
            new("1", "1 N. Hamilton St. @ McLaughlin Dr.,Jordan (KSQ725) (The fire bays face N. Hamilton St. while the ambulance bays face McLaughlin Dr.)"),
        }),
        new("25", "Kirkville V.F.C.", "6225 Kirkville Rd. N. between Poolsbrook Rd. & Erie Canal,Kirkville (KZV244)", new DepartmentStationSeed[]
        {
            new("1", "6225 Kirkville Rd. N. between Poolsbrook Rd. & Erie Canal,Kirkville (KZV244)"),
        }),
        new("26", "LaFayette V.F.D. & Amb.", "2444 US-11 S. between US-20 (Cherry Valley Trpk.) & Sturgeon Dr.,LaFayette (KQP688)", new DepartmentStationSeed[]
        {
            new("1", "2444 US-11 S. between US-20 (Cherry Valley Trpk.) & Sturgeon Dr.,LaFayette (KQP688)"),
            new("2", "5361 Rowland Rd. between US-20 (Cherry Valley Trpk.) & State Route 11A,LaFayette (KZV245)"),
        }),
        new("27", "Lakeside V.F.D.", "1002 State Fair Blvd. across from Cayuga St.,Syracuse (KQP683) (The Fire Sta. sits at the following coordinates: 43.099824/-76.247808, while the Fire Hall sits at 43.100304/-76.248197 but both have the same address)", new DepartmentStationSeed[]
        {
            new("1", "1002 State Fair Blvd. across from Cayuga St.,Syracuse (KQP683) (The Fire Sta. sits at the following coordinates: 43.099824/-76.247808, while the Fire Hall sits at 43.100304/-76.248197 but both have the same address)"),
            new("2", "7461 State Fair Blvd. (aka State Route 48) between O'Brien Rd. & Village Blvd. S.,Baldwinsville (Sta. is shared with the Baldwinsville VFD) (KNAI748)"),
        }),
        new("28", "Liverpool V.F.D.", "1110 Oswego St. (aka Oswego Rd./County Route 57) between Meyers Rd. & 7th St.,Liverpool (KLR345)", new DepartmentStationSeed[]
        {
            new("1", "1110 Oswego St. (aka Oswego Rd./County Route 57) between Meyers Rd. & 7th St.,Liverpool (KLR345)"),
            new("2", "1029 7th North St. between Electronics Pkwy. & Voorhies Ln.,Liverpool (KSQ726)"),
            new("3", "4089 Long Branch Rd. between Longdale Dr. & Marlton Cir.,Liverpool (KTQ273)"),
        }),
        new("30", "Lyncourt V.F.D.", "2909 Court St. (aka State Route 298) between Lyncourt Dr. & Brookland Dr.,Syracuse (KQP684)", new DepartmentStationSeed[]
        {
            new("1", "2909 Court St. (aka State Route 298) between Lyncourt Dr. & Brookland Dr.,Syracuse (KQP684)"),
        }),
        new("31", "Lysander V.F.D.", "664 Lamson Rd. between Plainville Rd. & Prine Rd.,Baldwinsville (KSQ727)", new DepartmentStationSeed[]
        {
            new("1", "664 Lamson Rd. between Plainville Rd. & Prine Rd.,Baldwinsville (KSQ727)"),
            new("2", "2100 Lamson Rd. between Oswego Rd. (aka State Route 48) & CSX RR Baldwinsville Subdivision,Lysander (WNJF599)"),
        }),
        new("32", "Manlius V.F.D. & Amb.", "8200 Cazenovia Rd. between Pompey Center Rd. and Enders Rd., Manlius (KQP687)", new DepartmentStationSeed[]
        {
            new("1", "8200 Cazenovia Rd. between Pompey Center Rd. and Enders Rd., Manlius (KQP687)"),
            new("1", "4 Stickley Dr. between Fayette St. (aka State Route 92) & Willowbrook Dr.,Manlius"),
        }),
        new("33", "Marcellus V.F.D.", "4242 Slate Hill Rd. between Pleasant Valley Rd. & Platt Rd.,Marcellus (KQP682) (Front of Sta. faces Lee Mulroy Rd. (aka State Route 174/State Route 175)", new DepartmentStationSeed[]
        {
            new("1", "4242 Slate Hill Rd. between Pleasant Valley Rd. & Platt Rd.,Marcellus (KQP682) (Front of Sta. faces Lee Mulroy Rd. (aka State Route 174/State Route 175)"),
        }),
        new("35", "Mattydale V.F.D.", "173 E. Molloy Rd. @ Mitchell Ave.,Mattydale (KZV248/WPVV638/WPXK826)", new DepartmentStationSeed[]
        {
            new("1", "173 E. Molloy Rd. @ Mitchell Ave.,Mattydale (KZV248/WPVV638/WPXK826)"),
        }),
        new("36", "Warners Memphis Fire Dist.", "1867 Cross St. @ Church St.,Memphis (KZV249) ***(Dept. merged w/Warners V.F.D. in July 2011)***", new DepartmentStationSeed[]
        {
            new("1", "1867 Cross St. @ Church St.,Memphis (KZV249) ***(Dept. merged w/Warners V.F.D. in July 2011)***"),
        }),
        new("37", "Minoa V.F.D. & Amb.", "238 N. Main St. (aka Costello Pkwy./Schepps Corners Rd.) @ Adams St.,Minoa (KSQ728)", new DepartmentStationSeed[]
        {
            new("1", "238 N. Main St. (aka Costello Pkwy./Schepps Corners Rd.) @ Adams St.,Minoa (KSQ728)"),
            new("2", "7036 Manlius Center Rd. (aka State Route 290) between Bowman Rd. & Eisenhower Ave.,East Syracuse (WNLQ694)"),
        }),
        new("38", "Mottville V.F.C. Inc.", "4149 Frost St. between Jordan Rd. & Oneil Ln.,Skaneateles (KLR342)", new DepartmentStationSeed[]
        {
            new("1", "4149 Frost St. between Jordan Rd. & Oneil Ln.,Skaneateles (KLR342)"),
        }),
        new("39", "Moyers Corners V.F.D.", "8481 Oswego Rd. (aka County Route 57) between State Route 31 & Linda Ln.,Baldwinsville (KQP681)", new DepartmentStationSeed[]
        {
            new("1", "8481 Oswego Rd. (aka County Route 57) between State Route 31 & Linda Ln.,Baldwinsville (KQP681)"),
            new("2", "7697 Morgan Rd. between Buckley Rd. & Forestbrook Dr.,Liverpool (KQP680)"),
            new("3", "7200 Henry Clay Blvd. @ W. Taft Rd.,Liverpool (KWN470) (The Sta. sits behind the gas station on the corner)"),
            new("4", "8044 Oswego Rd. (aka County Route 57) between Pine Hollow Rd. & Balboa Dr.,Liverpool (WNQD925)"),
        }),
        new("41", "Navarino V.F.D.", "3276 Amber Rd. between US-20 (Cherry Valley Trpk.) & Curtis Rd.,Syracuse (KSQ730)", new DepartmentStationSeed[]
        {
            new("1", "3276 Amber Rd. between US-20 (Cherry Valley Trpk.) & Curtis Rd.,Syracuse (KSQ730)"),
        }),
        new("42", "Nedrow V.F.D.", "6505 S. Salina St. (aka US-11) @ Rockwell Rd.,Syracuse (KLR340)", new DepartmentStationSeed[]
        {
            new("1", "6505 S. Salina St. (aka US-11) @ Rockwell Rd.,Syracuse (KLR340)"),
        }),
        new("43", "North Syracuse V.F.D.", "109 Chestnut St. between N. Main St. (aka Brewerton Rd./US-11) & George St.,North Syracuse (KLK512)", new DepartmentStationSeed[]
        {
            new("1", "109 Chestnut St. between N. Main St. (aka Brewerton Rd./US-11) & George St.,North Syracuse (KLK512)"),
            new("2", "70 General Irwin Blvd. (aka Thompson Rd.) between E. Taft Rd. & Stewart Dr.,North Syracuse (KYS807)"),
        }),
        new("44", "Onondaga Hill V.F.D.", "4831 Velasko Rd. between W. Seneca Trpk. (aka State Route 175) & Boyle Dr.,Syracuse (KLR346)", new DepartmentStationSeed[]
        {
            new("1", "4831 Velasko Rd. between W. Seneca Trpk. (aka State Route 175) & Boyle Dr.,Syracuse (KLR346)"),
        }),
        new("45", "Onondaga Nation V.F.D.", "3383 State Route 11A (aka Syracuse Tully Valley Rd.) just north of Hemlock Rd.,Nedrow (KZV247) (The Current Sta. sits at the following coordinates: 42.949278/-76.157887)", new DepartmentStationSeed[]
        {
            new("1", "3383 State Route 11A (aka Syracuse Tully Valley Rd.) just north of Hemlock Rd.,Nedrow (KZV247) (The Current Sta. sits at the following coordinates: 42.949278/-76.157887)"),
        }),
        new("46", "Otisco V.F.D.", "1933 State Route 80 between Canty Hill Rd. & Otisco Rd.,Tully (KZV252)", new DepartmentStationSeed[]
        {
            new("1", "1933 State Route 80 between Canty Hill Rd. & Otisco Rd.,Tully (KZV252)"),
        }),
        new("47", "Enterprise Fire Co. #1", "457 Main St. (aka County Route 57) between Lock St. & Bridge St.,Phoenix (KFG612) (Oswego Co.)", new DepartmentStationSeed[]
        {
            new("1", "457 Main St. (aka County Route 57) between Lock St. & Bridge St.,Phoenix (KFG612) (Oswego Co.)"),
            new("2", "42 Elm St. @ Lock St. (aka County Route 12),Phoenix (KNDL901) (Oswego Co.)"),
            new("3", "2929 Lamson Rd. between Pendergast Rd. & Sixty Rd.,Lysander (KLR343)"),
        }),
        new("48", "Plainville V.F.D.", "767 W. Genesee Rd. (aka State Route 370) between Plainville Rd. & Dog Harbor Rd.,Plainville (KQP678)", new DepartmentStationSeed[]
        {
            new("1", "767 W. Genesee Rd. (aka State Route 370) between Plainville Rd. & Dog Harbor Rd.,Plainville (KQP678)"),
            new("2", "6808 Plainville Rd. between Guyder Rd. & McIntyre Rd.,Memphis (KNAI743)"),
            new("3", "2021 W. Genesee Rd. (aka State Route 370) between I-690 & Emerick Rd.,Baldwinsville (KNAI742)"),
        }),
        new("51", "Pompey Hill V.F.D.", "7407 Academy St. between Henneberry Rd. & Sweet Rd.,Pompey (KQP677)", new DepartmentStationSeed[]
        {
            new("1", "7407 Academy St. between Henneberry Rd. & Sweet Rd.,Pompey (KQP677)"),
        }),
        new("52", "Seneca River V.F.D.", "3457 Hayes Rd. between Meadowbrook Dr. & Surbrook Rd.,Baldwinsville (KSQ721)", new DepartmentStationSeed[]
        {
            new("1", "3457 Hayes Rd. between Meadowbrook Dr. & Surbrook Rd.,Baldwinsville (KSQ721)"),
        }),
        new("53", "Sentinel Heights V.F.D.", "4200 Dave Tilden Rd. between Sentinel Heights Rd. & LaFayette Rd.,Jamesville (KLR347)", new DepartmentStationSeed[]
        {
            new("1", "4200 Dave Tilden Rd. between Sentinel Heights Rd. & LaFayette Rd.,Jamesville (KLR347)"),
        }),
        new("54", "Skaneateles V.F.D.", "77 W. Genesee St. (aka US-20) @ Kane Ave. (aka W. Lake Rd./State Route 41A),Skaneateles (KLK514) (The Bays face Kane Ave.)", new DepartmentStationSeed[]
        {
            new("1", "77 W. Genesee St. (aka US-20) @ Kane Ave. (aka W. Lake Rd./State Route 41A),Skaneateles (KLK514) (The Bays face Kane Ave.)"),
            new("2", "1642 Coon Hill Rd. between E. Lake Rd. (aka E. Lake St./State Route 41) & Rickard Rd.,Skaneateles (KNAW745)"),
            new("3", "1433 Lacy Rd. (aka State Route 359) between W. Lake Rd. (aka State Route 41A) & State Route 359,Skaneateles (KNAI741)"),
        }),
        new("57", "Solvay V.F.D.", "1925 Milton Ave. @ N. Orchard Rd.,Solvay (KNAI739)", new DepartmentStationSeed[]
        {
            new("1", "1925 Milton Ave. @ N. Orchard Rd.,Solvay (KNAI739)"),
            new("2", "1100 Cogswell Ave. @ Hazard St.,Solvay (KNAI740)"),
            new("4", "581 State Fair Blvd.,Syracuse (Located at the New York State Fair & only open during the Fair) (WNMR906) (Sta. sits inside the fairgrounds on Belle Isle Ave. between Tonawanda St. & Mohegan St. & at the following coordinates: 43.077302/-76.222986)"),
        }),
        new("58", "South Bay V.F.D.", "8817 Cicero Center Rd. between Lakeshore Rd. & Lyons Landing Rd.,Cicero (KUX358)", new DepartmentStationSeed[]
        {
            new("1", "8817 Cicero Center Rd. between Lakeshore Rd. & Lyons Landing Rd.,Cicero (KUX358)"),
        }),
        new("59", "South Onondaga V.F.D.", "3130 Cedarvale Rd. between Makyes Rd. & Red Mill Rd.,Nedrow (KQP686)", new DepartmentStationSeed[]
        {
            new("1", "3130 Cedarvale Rd. between Makyes Rd. & Red Mill Rd.,Nedrow (KQP686)"),
        }),
        new("60", "Southwood V.F.D.", "4581 Grace Pl. @ Clifford Dr.,Jamesville (KZV243)", new DepartmentStationSeed[]
        {
            new("1", "4581 Grace Pl. @ Clifford Dr.,Jamesville (KZV243)"),
        }),
        new("61", "Spafford V.F.D.", "660 E. Lake Rd. (State Route 41) @ Cold Brook Rd.,Homer (KZV242)", new DepartmentStationSeed[]
        {
            new("1", "660 E. Lake Rd. (State Route 41) @ Cold Brook Rd.,Homer (KZV242)"),
        }),
        new("62", "Taunton V.F.D.", "4300 Onondaga Blvd. between Fay Rd. & Bellevue Ave.,Syracuse (KSQ731)", new DepartmentStationSeed[]
        {
            new("1", "4300 Onondaga Blvd. between Fay Rd. & Bellevue Ave.,Syracuse (KSQ731)"),
            new("2", "4789 Harris Rd. between Old Homestead Rd. & W. Seneca Trpk. (aka State Route 175),Syracuse (KMA823)"),
        }),
        new("63", "Tully Hose Co. & Amb.", "1 Railroad St. @ Clinton St. (aka State Route 80),Tully (KQP676)", new DepartmentStationSeed[]
        {
            new("1", "1 Railroad St. @ Clinton St. (aka State Route 80),Tully (KQP676)"),
            new("2", "4845 State Route 80 between Dutch Hill Rd. & Mechanic St.,Tully (KNAI750) (Sta. is located in the Hamlet of Vesper)"),
        }),
        new("65", "Warners Memphis Fire Dist.", "6444 Newport Rd. between Warners Rd. (aka State Route 173) & Bentley Rd.,Camillus (KSQ733)", new DepartmentStationSeed[]
        {
            new("1", "6444 Newport Rd. between Warners Rd. (aka State Route 173) & Bentley Rd.,Camillus (KSQ733)"),
        }),
        new("66", "North Chittenango V.F.C.", "1699 Fyler Rd. (aka County Route 6) between Lakeport Rd. (aka County Route 3) & Apache Ln.,North Chittenango (KJH223) (Madison Co.)", new DepartmentStationSeed[]
        {
            new("1", "1699 Fyler Rd. (aka County Route 6) between Lakeport Rd. (aka County Route 3) & Apache Ln.,North Chittenango (KJH223) (Madison Co.)"),
        }),
        new("68", "Chittenango V.F.C.", "417 Genesee St. (aka State Route 5/State Route 13) @ Russell St.,Chittenango (KCI472) (Bays face Russell St.) (Madison Co.)", new DepartmentStationSeed[]
        {
            new("1", "417 Genesee St. (aka State Route 5/State Route 13) @ Russell St.,Chittenango (KCI472) (Bays face Russell St.) (Madison Co.)"),
        }),
        new("71", "Caughdenoy V.F.D. Inc.", "48 Prospect St. between Ladd St. & County Route 12,Central Square (KQP694) (Oswego Co.)", new DepartmentStationSeed[]
        {
            new("1", "48 Prospect St. between Ladd St. & County Route 12,Central Square (KQP694) (Oswego Co.)"),
            new("2", "1046 County Route 12 @ Aspen Rd.,Pennellville (KZV425/WPTC449) (Oswego Co.)"),
        }),
        new("72", "Cody V.F.D.", "155 County Route 55 between S. Granby Rd. & County Line Rd.,Fulton (KNDL900) (Oswego Co.)", new DepartmentStationSeed[]
        {
            new("1", "155 County Route 55 between S. Granby Rd. & County Line Rd.,Fulton (KNDL900) (Oswego Co.)"),
            new("2", "31 Wilcox Rd. between State Route 48 & County Route 14 (aka Ley Creek Rd.),Fulton (WNDA618) (Oswego Co.)"),
        }),
        new("79", "Syracuse Fire Department Ambulance/EMS", "808 Bellevue Ave. at Summit Ave. Syracuse, NY", new DepartmentStationSeed[]
        {
            new("3", "808 Bellevue Ave. at Summit Ave. Syracuse, NY"),
            new("2", "400 West Genesee St. at Wallace Ave. Syracuse, NY"),
        }),
        new("80", "East Area Volunteer Emergency Services (EAVES)", "6232 Fly Rd. between Hartwell Ave. & Swanka Rd.,East Syracuse", new DepartmentStationSeed[]
        {
            new("1", "6232 Fly Rd. between Hartwell Ave. & Swanka Rd.,East Syracuse"),
        }),
        new("81", "Greater Baldwinsville Ambulance Corps (GBAC)", "11 Albert Palmer Ln. between E. Genesee St. (aka State Route 31/State Route 370) & Elizabeth St.,Baldwinsville (KXL272/KQP831 - 45.960) / (KNAI744/KNAI745 - 46.140)", new DepartmentStationSeed[]
        {
            new("1", "11 Albert Palmer Ln. between E. Genesee St. (aka State Route 31/State Route 370) & Elizabeth St.,Baldwinsville (KXL272/KQP831 - 45.960) / (KNAI744/KNAI745 - 46.140)"),
        }),
        new("82", "North Area Volunteer Ambulance Corps (NAVAC)", "603 N. Main St. (aka Brewerton Rd./US-11) between Tuller Dr. & Highland Ave.,North Syracuse (KWX681 - 45.960) / (KZV250 - 46.140)", new DepartmentStationSeed[]
        {
            new("1", "603 N. Main St. (aka Brewerton Rd./US-11) between Tuller Dr. & Highland Ave.,North Syracuse (KWX681 - 45.960) / (KZV250 - 46.140)"),
        }),
        new("83", "Skaneateles Ambulance Volunteer Emergency Services (SAVES)", "77 Fennell St. between W. Austin St. & Sinclair St.,Skaneateles", new DepartmentStationSeed[]
        {
            new("1", "77 Fennell St. between W. Austin St. & Sinclair St.,Skaneateles"),
        }),
        new("87", "Western Area Volunteer Emergency Services (WAVES)", "202 Bennett Rd. @ Warners Rd. (aka State Route 173),Camillus (KKR625 - 45.960) / (KNAI749 - 46.140)", new DepartmentStationSeed[]
        {
            new("1", "202 Bennett Rd. @ Warners Rd. (aka State Route 173),Camillus (KKR625 - 45.960) / (KNAI749 - 46.140)"),
        }),
        new("88", "American Medical Response (AMR)", "101 Richmond Ave. @ Van Rensselaer St.,Syracuse (KED679)", new DepartmentStationSeed[]
        {
            new("1", "101 Richmond Ave. @ Van Rensselaer St.,Syracuse (KED679)"),
        }),
        new("89", "Northern Onondaga Volunteer Ambulance (NOVA)", "4425 Buckley Rd. between Morgan Rd. & John Glenn Blvd.,Liverpool (WPXK930 - 45.960) / (KNFU793 - 39STA1 / KNFU884 - 39STA2 / KNFU797 - 39STA3 / WNQD921 - 39STA4 - (46.140)", new DepartmentStationSeed[]
        {
            new("1", "4425 Buckley Rd. between Morgan Rd. & John Glenn Blvd.,Liverpool (WPXK930 - 45.960) / (KNFU793 - 39STA1 / KNFU884 - 39STA2 / KNFU797 - 39STA3 / WNQD921 - 39STA4 - (46.140)"),
        }),
        new("98", "Aircraft Rescue Fire Fighting (ARFF)", "110 Observation Cir. off of Air Cargo Rd.,Syracuse Airport (Airport) (KTC833/WPXZ438) - (154.995) / (KYS808) - (46.140) (aka SFD Sta. #4 and sits at the following coordinates: 43.111535/-76.115004))", new DepartmentStationSeed[]
        {
            new("1", "110 Observation Cir. off of Air Cargo Rd.,Syracuse Airport (Airport) (KTC833/WPXZ438) - (154.995) / (KYS808) - (46.140) (aka SFD Sta. #4 and sits at the following coordinates: 43.111535/-76.115004))"),
        }),
        new("99", "Syracuse Fire Dept. (see SFD Station List) (KEA331)", "100 College Pl. (#006) near Smith Dr.,Syracuse (Ambulance's sit at the following coordinates: 43.038173/-76.132544) (WNSU880)", new DepartmentStationSeed[]
        {
            new("1", "100 College Pl. (#006) near Smith Dr.,Syracuse (Ambulance's sit at the following coordinates: 43.038173/-76.132544) (WNSU880)"),
            new("2", "1000 Col. Eileen Collins Blvd.,Syracuse Airport (The Sta. sits at the following coordinates: 43.110965/-76.109955 & was formerly S.F.D. - Airport Sta. #4)"),
            new("1", "638 Burnet Ave. between Oak St. & N. Crouse Ave.,Syracuse"),
        }),
    };
}