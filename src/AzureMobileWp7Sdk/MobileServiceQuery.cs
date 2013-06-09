using System.Collections.Generic;

namespace AzuraMobileSdk
{
    public class MobileServiceQuery
    {
        private int _top;
        private int _skip;
        private string _orderby;
        private string _filter;
        private string _select;

        public MobileServiceQuery Top(int top)
        {
            _top = top;
            return this;
        }

        public MobileServiceQuery Skip(int skip)
        {
            _skip = skip;
            return this;
        }

        public MobileServiceQuery OrderBy(string orderby)
        {
            _orderby = orderby;
            return this;
        }

        public MobileServiceQuery Filter(string filter)
        {
            _filter = filter;
            return this;
        }

        public MobileServiceQuery Select(string select)
        {
            _select = select;
            return this;
        }

        public override string ToString()
        {
            var query = new List<string>();
            if (_top != 0)
            {
                query.Add("$top=" + _top);
            }
            if (_skip != 0)
            {
                query.Add("$skip=" + _skip);
            }
            if (!string.IsNullOrEmpty(_filter))
            {
                query.Add("$filter=" + _filter);
            }
            if (!string.IsNullOrEmpty(_select))
            {
                query.Add("$select=" + _select);
            }
            if (!string.IsNullOrEmpty(_orderby))
            {
                query.Add("$orderby=" + _orderby);
            }

            return string.Join("&", query);
        }
    }
}