using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Filter.Util;
using NUnit.Framework;

namespace Gallatin.Filter.Tests
{
    [TestFixture]
    public class IpAddressParserTests
    {
        [Test]
        public void IpAddressMatch()
        {
            Assert.That( IpAddressParser.IsMatch( "127.0.0.1", "127.0.0.1" ) );
            Assert.That(IpAddressParser.IsMatch("127.0.0.5", "127.0.0.1"), Is.False);

            Assert.That(IpAddressParser.IsMatch("127.0.0.2-127.0.0.6", "127.0.0.2"));
            Assert.That(IpAddressParser.IsMatch("127.0.0.2-127.0.0.6", "127.0.0.5"));
            Assert.That(IpAddressParser.IsMatch("127.0.0.2-127.0.0.6", "127.0.0.6"));
            Assert.That(IpAddressParser.IsMatch("127.0.0.2-127.0.0.6", "127.0.0.7"), Is.False);

            Assert.That(IpAddressParser.IsMatch("127.0.0.2/16", "127.0.0.2"));
            Assert.That(IpAddressParser.IsMatch("127.1.0.0/16", "127.0.0.2"), Is.False);

        }
    }
}
