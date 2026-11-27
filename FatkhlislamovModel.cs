using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChudoShoes
{
    public partial class ChudoShoesFatkhlislamovDBEntities
    {
        private static ChudoShoesFatkhlislamovDBEntities _context;

        public static ChudoShoesFatkhlislamovDBEntities GetContext()
        {
            if (_context == null)
            {
                _context = new ChudoShoesFatkhlislamovDBEntities();
            }
            return _context;
        }
    }
}