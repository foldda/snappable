using Charian;
using Foldda.DataAutomation.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace UnitTests
{
    [TestClass]
    public class UniversalDataFrameworkTests
    {

        [TestMethod]
        public void TestMdv1()
        {
            // test ContainerHeader getters
            Rda mdvHeader = Rda.Parse(@"|;,\|sec1|sec2");
            //normal
            Assert.AreEqual("sec1", mdvHeader.GetValue(new int[] { 0 }));
            Assert.AreEqual("sec2", mdvHeader.GetValue(new int[] { 1 }));

            //.. first child-index, if there is no delimiter in the value
            //imaging there were delimiters at the end of the value, but they were trimmed when being encoded
            //eg "sec1,;" as "sec1"
            Assert.AreEqual("sec1", mdvHeader.GetValue(new int[] { 0, 0 }));
            Assert.AreEqual("sec1", mdvHeader.GetValue(new int[] { 0, 0, 0 }));
            Assert.AreEqual("sec2", mdvHeader.GetValue(new int[] { 1, 0 }));

            //index not valid ...
            Assert.IsNull(mdvHeader.GetValue(new int[] { 0, 0, 0, 0 }));
            Assert.AreEqual(string.Empty, mdvHeader.GetValue(new int[] { 0, 0, 1 }));
            Assert.AreEqual(string.Empty, mdvHeader.GetValue(new int[] { 5, 0 }));

            // test ContainerHeader setters
            mdvHeader.SetValue(new int[] { 0 }, "SEC1");
            mdvHeader.SetValue(new int[] { 1 }, "SEC2");
            mdvHeader.SetValue(new int[] { 2 }, "SEC3");
            Assert.AreEqual("|;,\\|SEC1|SEC2|SEC3", mdvHeader.ToString());

            mdvHeader.SetValue(new int[] { 0, 1 }, "SEC1b");
            Assert.AreEqual("|;,\\|SEC1;SEC1b|SEC2|SEC3", mdvHeader.ToString());

            mdvHeader.SetValue(new int[] { 0, 0, 1 }, "SEC1c");
            Assert.AreEqual("|;,\\|SEC1,SEC1c;SEC1b|SEC2|SEC3", mdvHeader.ToString());

            mdvHeader.SetValue(new int[] { 0, 1, 1 }, "SEC1d");
            Assert.AreEqual("|;,\\|SEC1,SEC1c;SEC1b,SEC1d|SEC2|SEC3", mdvHeader.ToString());
            Assert.AreEqual("SEC1,SEC1c;SEC1b,SEC1d", mdvHeader.GetValue(new int[] { 0 }));
            Assert.AreEqual("SEC1,SEC1c", mdvHeader.GetValue(new int[] { 0, 0 }));
            Assert.AreEqual("SEC1d", mdvHeader.GetValue(new int[] { 0, 1, 1 }));

            mdvHeader.SetValue(new int[] { 0, 1, 4 }, "SE;|C1d4");
            Assert.AreEqual("|;,\\|SEC1,SEC1c;SEC1b,SEC1d,,,SE\\;\\|C1d4|SEC2|SEC3", mdvHeader.ToString());    //Raw encoded header string
            Assert.AreEqual("SE;|C1d4", mdvHeader.GetValue(new int[] { 0, 1, 4 }));
            //test turple encoder chars
            var section = mdvHeader.GetValue(new int[] { 0 });
            Assert.AreEqual("SEC1,SEC1c;SEC1b,SEC1d,,,SE\\;\\|C1d4", section);
            Assert.AreEqual(';', mdvHeader.EncodingDelimiters[0]);
            Assert.AreEqual(',', mdvHeader.EncodingDelimiters[1]);
            Assert.AreEqual(2, mdvHeader.EncodingDelimiters.Length);
            Assert.AreEqual('\\', mdvHeader.EscapeChar);
            //test default value
            Assert.AreEqual(string.Empty, mdvHeader.GetValue(new int[] { 0, 1, 3 }));
            Assert.AreEqual(string.Empty, mdvHeader.GetValue(new int[] { 0, 1, 8 }));  //non-exist
            Assert.IsNull(mdvHeader.GetValue(new int[] { 0, 1, 0, 0 }));   //over (dimension) index
            Assert.AreEqual("SEC1d", mdvHeader.GetValue(new int[] { 0, 1, 1 }));
            Assert.AreEqual("SEC1d", mdvHeader.GetValue(new int[] { 0, 1, 1 }));   //test the wrapper

            mdvHeader.SetValue(new int[] { 0, 1, 4, 2, 1 }, "Test Over-Index set");
            Assert.AreEqual("|;,\\|SEC1,SEC1c;SEC1b,SEC1d,,,SE\\;\\|C1d4|SEC2|SEC3", mdvHeader.ToString());   //Raw encoded header string - expect no-change

            string[] r1 = mdvHeader[0][1][1].ChildrenValueArray;//.//GetChildValues(new int[] { 0, 1, 1 }/*parent addr*/);
            Assert.AreEqual(1, r1.Length); 
            Assert.AreEqual("SEC1d", r1[0]);

            string[] r2 = mdvHeader[0].ChildrenValueArray;
            Assert.AreEqual(2, r2.Length);
            Assert.AreEqual("SEC1,SEC1c", r2[0]);
            Assert.AreEqual("SEC1b,SEC1d,,,SE\\;\\|C1d4", r2[1]);

            string[] r2a = mdvHeader[1].ChildrenValueArray;
            Assert.AreEqual(1, r2a.Length);
            Assert.AreEqual("SEC2", r2a[0]);

            string[] r3 = mdvHeader[0][1].ChildrenValueArray; 
            Assert.AreEqual(5, r3.Length);
            Assert.AreEqual("SEC1b", r3[0]);
            Assert.AreEqual("SEC1d", r3[1]);
            Assert.AreEqual(string.Empty, r3[3]);
            Assert.AreEqual("SE\\;\\|C1d4", r3[4]);
        }


        [TestMethod]
        public void TestMdv2()
        {
            Rda header = new Rda();

            //
            header.SetValue(new int[] { 0 }, "SEC1");
            Assert.AreEqual("SEC1", header.GetValue(new int[] { 0 }));
            header.SetValue(new int[] { 1 }, "SEC2");
            header.SetValue(new int[] { 2 }, "SEC3");

            // test ContainerHeader setters
            header.SetValue(new int[] { 0, 1 }, "SEC1b");
            Assert.AreEqual("|;,\\|SEC1;SEC1b|SEC2|SEC3", header.ToString());
            header.SetValue(new int[] { 0, 0, 1 }, "SEC1c");
            Assert.AreEqual("|;,\\|SEC1,SEC1c;SEC1b|SEC2|SEC3", header.ToString());
            header.SetValue(new int[] { 0, 1, 1 }, "SEC1d");
            Assert.AreEqual("|;,\\|SEC1,SEC1c;SEC1b,SEC1d|SEC2|SEC3", header.ToString());

            header.SetValue(new int[] { 0, 1, 4 }, "SEC1d4");
            Assert.AreEqual("|;,\\|SEC1,SEC1c;SEC1b,SEC1d,,,SEC1d4|SEC2|SEC3", header.ToString());
            Assert.AreEqual("SEC1,SEC1c;SEC1b,SEC1d,,,SEC1d4", header.GetValue(new int[] { 0 }));
            Assert.AreEqual(string.Empty, header.GetValue(new int[] { 0, 1, 3 }));
            Assert.AreEqual(string.Empty, header.GetValue(new int[] { 0, 1, 8 }));  //non-exist
            Assert.IsNull(header.GetValue(new int[] { 0, 1, 0, 0 }));   //over (dimension) index
            Assert.AreEqual("SEC1d", header.GetValue(new int[] { 0, 1, 1 }));

            //test over-indexing
            header.SetValue(new int[] { 0, 1, 4, 2, 1 }, "Test Over-Index set");
            Assert.AreEqual("|;,\\|SEC1,SEC1c;SEC1b,SEC1d,,,SEC1d4|SEC2|SEC3", header.ToString());   //expect no-change

            //test escape/un-escape
            string input = "SEC2,;|d3"; //contains delimiters
            header.SetValue(new int[] { 0, 2 }, input);
            //raw encoded string contains delimiters (and escaped delimiters)
            Assert.AreEqual("|;,\\|SEC1,SEC1c;SEC1b,SEC1d,,,SEC1d4;SEC2\\,\\;\\|d3|SEC2|SEC3", header.ToString());
            //values are restored when un-escape is applied
            Assert.AreEqual(input, header.GetValue(new int[] { 0, 2 }));
        }

        [TestMethod]
        public void TestMdv3()
        {
            Rda header = new Rda();

            //
            header.SetValue(new int[] { 0 }, "SEC1");
            Assert.AreEqual("SEC1", header.GetValue(new int[] { 0 }));
            header[1].ChildrenValueArray=new string[] { "S1a", "S1b", "S1c", "S1d" };
            Assert.AreEqual(header[1].ScalarValue, string.Join(header.EncodingDelimiters[1].ToString(), header[1].ChildrenValueArray));
            Assert.AreEqual("|;,\\|SEC1|S1a;S1b;S1c;S1d", header.ToString());
        }
    }
}
