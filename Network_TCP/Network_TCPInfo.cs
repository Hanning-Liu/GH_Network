using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace Network_TCP
{
    public class Network_TCPInfo : GH_AssemblyInfo
    {
        public override string Name => "Network";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("daf5e8be-5b42-4c91-9bce-d99f6b020aaa");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}