using System;


class Elastic : Ease { 
    public static double EaseIn(double t, double b, double c, double d)
    {
        double a = 0;
        double p = d * .3;
        double s;
	    if (t==0) return b;  if ((t/=d)==1) return b+c;
	    if (a < Math.Abs(c)) { a=c; s = p/4; }
        else
            s = p / TWO_PI * Math.Asin(c / a);
        return -(a * Math.Pow(2, 10 * (t -= 1)) * Math.Sin((t * d - s) * TWO_PI / p)) + b;
    }
    public static double EaseOut(double t, double b, double c, double d)
    {
        double a = 0;
        double p = d*.3;
        double s;
	    if (t==0) return b;  if ((t/=d)==1) return b+c;
	    if (a < Math.Abs(c)) { a=c; s = p/4; }
        else
            s = p / TWO_PI * Math.Asin(c / a);
        return (a * Math.Pow(2, -10 * t) * Math.Sin((t * d - s) * TWO_PI / p) + c + b);
    }
    public static double EaseInOut(double t, double b, double c, double d)
    {
        double a = 0;
        double p = d * (.3 * 1.5);
        double s;
	    if (t==0) return b;  if ((t/=d/2)==2) return b+c;
	    if (a < Math.Abs(c)) { a=c; s = p/4; }
        else
            s = p / TWO_PI * Math.Asin(c / a);
        if (t < 1)
            return -.5 * (a * Math.Pow(2, 10 * (t -= 1)) * Math.Sin((t * d - s) * TWO_PI / p)) + b;
        return a * Math.Pow(2, -10 * (t -= 1)) * Math.Sin((t * d - s) * TWO_PI / p) * .5 + c + b;
    }


    public static double EaseIn(double t, double b, double c, double d, double a, double p)
    {
        double s;
        if (t == 0)
            return b;
        if ((t /= d) == 1)
            return b + c;
        if (p == 0)
            p = d * .3;
        if (a < Math.Abs(c))
        {
            a = c;
            s = p / 4;
        }
        else
            s = p / TWO_PI * Math.Asin(c / a);
        return -(a * Math.Pow(2, 10 * (t -= 1)) * Math.Sin((t * d - s) * TWO_PI / p)) + b;
    }
    public static double EaseOut(double t, double b, double c, double d, double a, double p)
    {
        double s;
        if (t == 0)
            return b;
        if ((t /= d) == 1)
            return b + c;
        if (p == 0)
            p = d * .3;
        if (a < Math.Abs(c))
        {
            a = c;
            s = p / 4;
        }
        else
            s = p / TWO_PI * Math.Asin(c / a);
        return (a * Math.Pow(2, -10 * t) * Math.Sin((t * d - s) * TWO_PI / p) + c + b);
    }
    public static double EaseInOut(double t, double b, double c, double d, double a, double p)
    {
        double s;
        if (t == 0)
            return b;
        if ((t /= d / 2) == 2)
            return b + c;
        if (p == 0)
            p = d * (.3 * 1.5);
        if (a < Math.Abs(c))
        {
            a = c;
            s = p / 4;
        }
        else
            s = p / TWO_PI * Math.Asin(c / a);
        if (t < 1)
            return -.5 * (a * Math.Pow(2, 10 * (t -= 1)) * Math.Sin((t * d - s) * TWO_PI / p)) + b;
        return a * Math.Pow(2, -10 * (t -= 1)) * Math.Sin((t * d - s) * TWO_PI / p) * .5 + c + b;
    }
}
