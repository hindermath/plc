CONST x = 5;
VAR squ;

PROCEDURE square;
BEGIN
   squ:= x * x
END;

BEGIN
    CALL square;
    ! squ;
END.
