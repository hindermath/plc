VAR n, f;

PROCEDURE fact;
BEGIN
  IF n > 1 THEN
  BEGIN
    f := n * f;
    n := n - 1;
    CALL fact
  END
END;

BEGIN
   ? "Enter a number " n; 
   f := 1; CALL fact; !f
END.
